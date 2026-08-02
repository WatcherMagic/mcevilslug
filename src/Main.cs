using BepInEx;
using mcevilslug;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using On.MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using RWCustom;
using UnityEngine;

namespace SlugTemplate
{
    [BepInPlugin(MOD_ID, "Evil McEvilslug", "0.1.0")]
    
    [BepInDependency("dynamicpupspawns", BepInDependency.DependencyFlags.HardDependency)]

    class Main : BaseUnityPlugin
    {   
        public const string MOD_ID = "mcevilslug";

        private const int QUARTER_FOOD_AMOUNT_MUSHROOM = 2;
        private const int FOOD_AMOUNT_KARMAFLOWER = 1;

        private const int ROUGH_GRAB_AND_MAUL_COUNTER = 20;
        private const int SLUG_TO_BACK_COUNTER = 10;

        private const int PUP_CHUNK_GRAB = 0;
        
        private const float _CANT_SNIFF_SECONDS = 15f; //15 or 20
        private float _sinceLastSniffSeconds = _CANT_SNIFF_SECONDS;
        private const float _SNIFF_HOLD_SECONDS = 1f;
        private float _sniffHeldSeconds = 0f;
        private bool _setNotifySound = false;
        private bool _sniffing = false;
        private const float _SNIFF_DURATION = 1.5f;
        private float _sniffedFor = 0f;

        Dictionary<int, float> _pupRoomTimes = new();
        private float _worldTime = 0f;
        private int _maxVoidSpawn = 15;
        private int _minVoidSpawn = 1;
        private float _maxAge = 400f;

        public ConditionalWeakTable<Player, EvilSlugcatData> evilPupData = new ();

        internal static BindingFlags bfAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public void OnEnable()
        {
            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);

            //register and unregister enums
            //On.RainWorld.OnModsEnabled += RainWorld_OnModsEnabled;
            EvilEnums.RegisterValues();
            On.RainWorld.OnModsDisabled += RainWorld_OnModsDisabled;

            //world hooks
            On.World.SpawnPupNPCs += ClearPupTrackingValues;
            On.RainWorldGame.Update += IncrementWorldTime;

            //player hooks
            On.Player.ObjectEaten += AddFood;
            On.Player.GrabUpdate += EvilGrabUpdate;
            On.Player.ThrownSpear += EvilSpearThrow;
            On.Player.Update += EvilPlayerUpdates;
            //On.Player.Jump += LogSoundErrors;
            On.Player.MovementUpdate += EvilClimb;
            On.Player.UpdateBodyMode += TryClimbing;
            
            //THIS BREAKS CARRYING & ROUGH GRABBING
            //On.Player.IsCreatureImmuneToPlayerGrabStun += RoughGrabDoesntStun;
            
            //slugnpc hooks
            SlugNPCAI.Move += PupMove;
            SlugNPCAI.Update += PupUpdate;
            SlugNPCAI.DecideBehavior += PupBehavior;

            try
            {
                IL.Player.Update += NoPopcorn;
                IL.Player.UpdateBodyMode += NegateWallslide;
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        private void IncrementWorldTime(On.RainWorldGame.orig_Update orig, RainWorldGame self)
        {
            orig(self);
            
            if (!self.paused)
            {
                _worldTime += Time.deltaTime;
            }
        }

        private int ClearPupTrackingValues(On.World.orig_SpawnPupNPCs orig, World self)
        {
            _pupRoomTimes.Clear();
            _worldTime = 0f;
            
            return orig(self);
        }

        private void EvilClimb(On.Player.orig_MovementUpdate orig, Player self, bool eu)
        {
            orig(self, eu);

            if (self.bodyMode == Player.BodyModeIndex.WallClimb)
            {
                self.wallSlideCounter = 0;
                
                //skid to stop when grabbing wall while falling/stick to walls
                self.bodyChunks[0].vel.y = Mathf.Lerp(self.bodyChunks[0].vel.y, 0f, 0.1f);
            }
        }
        
        private void TryClimbing(On.Player.orig_UpdateBodyMode orig, Player self)
        {
            orig(self);
            
            //Debug.Log("[evilslug] body mode: " + self.bodyMode);
            
            //enter climb state
            if (self.slugcatStats.name.value == MOD_ID
                    && self.bodyMode != Player.BodyModeIndex.WallClimb
                    && self.State.alive
                    && !self.Stunned
                    && self.bodyMode != Player.BodyModeIndex.CorridorClimb
                    && self.bodyMode != Player.BodyModeIndex.Swimming
                    && self.input[0].y > 0
                //jump onto wall from ground
                && (self.bodyChunks[0].ContactPoint.x != 0
                        && self.input[0].jmp
                    //grab wall midair
                    || (self.bodyChunks[0].ContactPoint.x != 0
                        || self.bodyChunks[1].ContactPoint.x != 0)
                        && self.bodyChunks[0].contactPoint.y == 0)
                        && self.bodyChunks[1].ContactPoint.y == 0)
            {
                self.bodyMode = Player.BodyModeIndex.WallClimb;
            }
            
            //exit climbing mode (already handled by game)
            // if (self.input[0].jmp || (self.bodyChunks[0].contactPoint.x == 0 //suspended in air
            //                           && self.bodyChunks[1].contactPoint.x == 0
            //                           && self.bodyChunks[1].contactPoint.y == 0)
            //                       //on ground
            //                       || self.bodyChunks[0].contactPoint.y < 0)
            // {
            //     self.bodyMode = Player.BodyModeIndex.Default;
            //     return;
            // }
        }

        private void LogSoundErrors(On.Player.orig_Jump orig, Player self)
        {
            orig(self);
            
            SoundLoader loader = self.room.world.game.rainWorld.processManager.soundLoader;
            string soundErrors = "Sound Loader Errors:";
            foreach (string s in loader.errors)
            {
                soundErrors += "\n" + s;
            }
            Logger.LogInfo(soundErrors);
        }

        private void LoadResources(RainWorld rainWorld)
        {
            Futile.atlasManager.LoadImage("atlases/puptracks/pup_track");
        }

        // private void RainWorld_OnModsEnabled(On.RainWorld.orig_OnModsEnabled orig,
        //     RainWorld self, ModManager.Mod[] newlyEnabledMods)
        // {
        //     orig(self, newlyEnabledMods);
        //     EvilEnums.RegisterValues();
        // }

        private void RainWorld_OnModsDisabled(On.RainWorld.orig_OnModsDisabled orig,
            RainWorld self, ModManager.Mod[] newlyDisabledMods)
        {
            orig(self, newlyDisabledMods);
            foreach (ModManager.Mod mod in newlyDisabledMods)
                if (mod.id == MOD_ID)
                    EvilEnums.UnregisterValues();
        }

        private void AddFood(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {
            if (self.slugcatStats.name.value == MOD_ID)
            {
                if (edible.GetType().Name == nameof(KarmaFlower))
                {
                    self.AddFood(FOOD_AMOUNT_KARMAFLOWER);
                }
                if (edible.GetType().Name == nameof(Mushroom))
                {
                    for (int i = QUARTER_FOOD_AMOUNT_MUSHROOM; i > 0; i--)
                    {
                        self.AddQuarterFood();
                    }
                }
            }

            orig(self, edible);
        }

        private void EvilGrabUpdate(On.Player.orig_GrabUpdate orig, Player self, bool eu)
        {
            EvilSlugcatData evilslug = evilPupData.GetOrCreateValue(self);
            
            if (self.input[0].pckp && self.grasps[0] != null &&
            self.slugcatStats.name.value == MOD_ID
            && self.grasps[0].grabbed is Creature crit
            && !crit.dead
            && crit is Player slugcat)
            {
                evilslug.FramesPickupHeld += 1;
                EvilSlugcatData pupData = evilPupData.GetOrCreateValue(slugcat);
                
                if (self.input[1].AnyDirectionalInput && !self.input[0].AnyDirectionalInput)
                {
                    evilslug.FramesSlugToBackInput = 0;
                }
                if (self.input[0].AnyDirectionalInput)
                {
                    evilslug.FramesSlugToBackInput += 1;
                    if (evilslug.FramesSlugToBackInput >= SLUG_TO_BACK_COUNTER)
                    {
                        Logger.LogInfo("Placing pup on back");
                        pupData.BeingRoughGrabbed = false;
                        
                        self.slugOnBack.SlugToBack(slugcat);
                        evilslug.FramesSlugToBackInput = 0;
                        evilslug.FramesPickupHeld = 0;
                    }
                }
                else if (evilslug.FramesPickupHeld >= ROUGH_GRAB_AND_MAUL_COUNTER && !pupData.BeingRoughGrabbed)
                {
                    pupData.BeingRoughGrabbed = true;
                    //slugcat.standing = false;
                    self.Grab(slugcat, 0, PUP_CHUNK_GRAB, Creature.Grasp.Shareability.CanNotShare, 0.5f, true, true);
                    
                    evilslug.FramesPickupHeld = 0;
                }
                else if (pupData.BeingRoughGrabbed && evilslug.FramesPickupHeld >= ROUGH_GRAB_AND_MAUL_COUNTER)
                {
                    self.MaulingUpdate(0);
                    pupData.BeingRoughGrabbed = false;
                    evilslug.FramesPickupHeld = 0;
                }
            }
            else
            {
                evilslug.FramesPickupHeld = 0;
            }
            
            orig(self, eu);
        }

        private void EvilSpearThrow(On.Player.orig_ThrownSpear orig, Player self, Spear spear)
        {
            orig(self, spear);

            if (self.slugcatStats.name.value == MOD_ID)
            {
                BodyChunk firstChunk = spear.firstChunk;

                firstChunk.vel.y *= UnityEngine.Random.Range(0f, 1f);
                firstChunk.vel.x *= UnityEngine.Random.Range(-1f, 1f);
                spear.SetRandomSpin();
            }
        }

        private void NoPopcorn(ILContext il)
        {
            try
            {
                var c = new ILCursor(il);

                c.GotoNext(MoveType.After, 
                    x => x.MatchSub(),
                    x => x.MatchStfld(typeof(Player).GetField(nameof(Player.eatExternalFoodSourceCounter))),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld(typeof(Player).GetField(nameof(Player.eatExternalFoodSourceCounter))),
                    x => x.MatchLdcI4(1),
                    x => x.MatchBge(out _));
                ILLabel emitTarget = c.MarkLabel();

                c.GotoNext(MoveType.Before,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdcI4(1),
                    x => x.MatchCallOrCallvirt(typeof(Player).GetMethod(nameof(Player.AddFood))),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdcI4(0x2D)); //int32 45
                c.Index += 3;
                ILLabel skipFoodTarget = c.MarkLabel();

                c.GotoLabel(emitTarget);
                c.Emit(OpCodes.Ldarg_0); //load the Player instance onto the stack.
                c.EmitDelegate<Func<Player, bool>>((self) =>
                { // Func will similarly follow the stack, the last type will be your return type
                    if (self.slugcatStats.name.value == MOD_ID) // Change this to whatever your code is
                    {
                        return true;
                    }
                    return false;
                });
                // Since the return type is on the stack we can use it here
                c.Emit(OpCodes.Brtrue, skipFoodTarget);


                //Logger.LogInfo(il.ToString());
                //UnityEngine.Debug.Log(il.ToString());

            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
        
        private void NegateWallslide(ILContext il)
        {
            try
            {
                //var c = new ILCursor(il);

                Func<Instruction, bool>[] instructions =
                {
                    x => x.MatchLdarg(0),
                    x => x.MatchCall<Player>(nameof(Player.input)),
                    x => x.MatchLdcI4(0),
                    x => x.MatchLdelema(typeof(Player.InputPackage)),
                    x => x.MatchLdfld<Player.InputPackage>(nameof(Player.InputPackage.x)),
                    x => x.MatchBrfalse(out _),
                    
                    x => x.MatchLdarg(0),
                    x => x.MatchLdcI4(0),
                    x => x.MatchLdarg(0),
                    x => x.MatchCall<Player>(nameof(Player.input)),
                    x => x.MatchLdcI4(0),
                    x => x.MatchLdelema(typeof(Player.InputPackage)),
                    x => x.MatchLdfld<Player.InputPackage>(nameof(Player.InputPackage.x)),
                    x => x.MatchLdcI4(0),
                    x => x.MatchCall(typeof(PhysicalObject).GetMethod(nameof(PhysicalObject.IsTileSolid),
                        new[] { typeof(int), typeof(int), typeof(int) }))
                };
               // Logger.LogInfo(ILDebug.IterateEntireILContext(il, instructions));

                // ILLabel skipGravityBlock = c.DefineLabel();
                //
                // c.GotoNext(MoveType.Before,
                //     x => x.MatchLdarg(0),
                //     x => x.MatchCall<Player>(nameof(Player.input)),
                //     x => x.MatchLdcI4(0),
                //     x => x.MatchLdelema(typeof(Player.InputPackage)),
                //     x => x.MatchLdfld<Player.InputPackage>(nameof(Player.InputPackage.x)),
                //     x => x.MatchBrfalse(out skipGravityBlock),
                //
                //     x => x.MatchLdarg(0),
                //     x => x.MatchLdcI4(0),
                //     x => x.MatchLdarg(0),
                //     x => x.MatchCall<Player>(nameof(Player.input)),
                //     x => x.MatchLdcI4(0),
                //     x => x.MatchLdelema(typeof(Player.InputPackage)),
                //     x => x.MatchLdfld<Player.InputPackage>(nameof(Player.InputPackage.x)),
                //     x => x.MatchLdcI4(0),
                //     x => x.MatchCall(typeof(PhysicalObject).GetMethod(nameof(PhysicalObject.IsTileSolid), new[] { typeof(int), typeof(int), typeof(int) })));
                // c.MoveAfterLabels();
                // c.EmitDelegate(() =>
                // {
                //     Debug.Log("[evilslug] this is where wall slide gravity happens");
                // });
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        private void EvilPlayerUpdates(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            _sinceLastSniffSeconds += Time.deltaTime;

            TrackRoomsPupsEntered(self);
            
            Sniff(self);
            
            UpdateRoughGrabState(self);

            if (_sniffing)
            {
                SniffUpdate(self);
            }
        }

        private void UpdateRoughGrabState(Player self)
        {
            if (evilPupData.TryGetValue(self, out var pup))
            {
                if (self.grabbedBy.Count == 0)
                {
                    pup.BeingRoughGrabbed = false;
                }
            }
        }

        private void TrackRoomsPupsEntered(Player self)
        {
            try
            {
                if (!self.dead
                    && self.isNPC
                    && !self.AI.abstractAI.isTamed)
                {
                    _pupRoomTimes[self.abstractCreature.Room.index] = _worldTime;
                }
            }
            catch (Exception e)
            {
                Logger.LogError("An exception was caught and logged in TrackRoomsPupsEntered");
                Debug.LogException(e);
            }
        }
        
        private void Sniff(Player self)
        {
            if (self.slugcatStats.name.value == MOD_ID)
            {
                if ((ModManager.Watcher && self.input[0].spec)
                    || (self.input[0].pckp && self.input[0].mp))
                {
                    _sniffHeldSeconds += Time.deltaTime;
                    
                    if (_sinceLastSniffSeconds >= _CANT_SNIFF_SECONDS && _sniffHeldSeconds >= _SNIFF_HOLD_SECONDS)
                    {
                        Debug.Log("[McEvil] Sniffing");
                        _sniffing = true;
                        _sinceLastSniffSeconds = 0f;
                        _setNotifySound = true;
                        self.Blink(100);
                        if (self.room != null)
                        {
                            self.room.PlaySound(EvilEnums.EvilSniff);
                            if (_pupRoomTimes.TryGetValue(self.room.abstractRoom.index, out float time))
                            {
                                float age = _worldTime - time;
                                PupTrailEffects(self.room, self.mainBodyChunk.pos, age);
                            }
                        }
                    }
                    else
                    {
                        //Debug.Log("[McEvil] Can't sniff yet!");
                        //self.Blink(10);
                    }
                }
                else
                {
                    _sniffHeldSeconds = 0f;
                }
                if (_sinceLastSniffSeconds >= _CANT_SNIFF_SECONDS && _setNotifySound)
                {
                    self.room.PlaySound(SoundID.Token_Collect);
                    _setNotifySound = false;
                }
            }
        }
        
        private void PupTrailEffects(Room room, Vector2 worldPos, float age)
        {
            float fade = Mathf.InverseLerp(_maxAge, 0, age);
            int numSpawn = Mathf.RoundToInt(Mathf.Lerp(_minVoidSpawn, _maxVoidSpawn, fade));
            Vector2 spawnPos;
            
            for (int i = numSpawn; i > 0; i--)
            {
                spawnPos = worldPos + Custom.RNV() * UnityEngine.Random.Range(20f, 100f);
                
                AbstractPhysicalObject abs = new (
                    room.world,
                    AbstractPhysicalObject.AbstractObjectType.VoidSpawn,
                    null,
                    room.GetWorldCoordinate(spawnPos),
                    room.game.GetNewID());
                VoidSpawn spawn = new VoidSpawn(abs, room.roomSettings.GetEffectAmount(RoomSettings.RoomEffect.Type.VoidMelt), false);
                spawn.fade = fade;
                spawn.behavior = new VoidSpawn.CircleSwarm(spawn, room);
                
                abs.RealizeInRoom();
            }
        }

        private void SniffUpdate(Player self)
        {
            _sniffedFor += Time.deltaTime;
            if (_sniffedFor >= _SNIFF_DURATION)
            {
                _sniffing = false;
            }
            
            if (self.graphicsModule != null)
            {
                GenericBodyPart head = (self.graphicsModule as PlayerGraphics).head;
                head.vel += Custom.RNV() * (UnityEngine.Random.value * 1.5f);
            }
        }
        
        private bool RoughGrabDoesntStun(On.Player.orig_IsCreatureImmuneToPlayerGrabStun orig, Player self, Creature grabCheck)
        {
            return (self.slugcatStats.name.value == MOD_ID && grabCheck is Player) ? false : orig(self, grabCheck);
        }
        
        private void PupMove(SlugNPCAI.orig_Move orig, MoreSlugcats.SlugNPCAI self)
        {
            orig(self);
        }
        
        private void PupUpdate(SlugNPCAI.orig_Update orig, MoreSlugcats.SlugNPCAI self)
        {
            orig(self);

            if (evilPupData.TryGetValue(self.cat, out var pup))
            {
                if (pup.LosingTrust
                    && self.friendTracker.friend != null
                    && self.friendTracker.friend is Player slugcat
                    && slugcat.slugcatStats.name.value == MOD_ID)
                {
                    self.cat.State.socialMemory.GetOrInitiateRelationship(slugcat.abstractCreature.ID).InfluenceTempFear(0.1f);
                }
            }
        }
        
        private void PupBehavior(SlugNPCAI.orig_DecideBehavior orig, MoreSlugcats.SlugNPCAI self)
        {
            orig(self);

            if (evilPupData.TryGetValue(self.cat, out var pup))
            {
                if (self.cat.grabbedBy.Count > 0
                    && self.cat.grabbedBy[0].grabber is Player player
                    && player.slugcatStats.name.value == MOD_ID
                    && pup.BeingRoughGrabbed)
                {
                    
                    if (EvilslugIsFriend(self))
                    {
                        pup.LosingTrust = true;
                    
                        //copied from slugnpc ai
                        if (self.heldWiggle == 0 && (double) self.creature.personality.energy > 0.6000000238418579 && (double) UnityEngine.Random.value < (double) Custom.LerpMap(self.creature.personality.energy, 0.6f, 1f, 1f / 400f, 0.01f))
                            self.heldWiggle = Mathf.RoundToInt((float) UnityEngine.Random.Range(100, 300));
                    }
                    else
                    {
                        pup.LosingTrust = false;
                    }
                }
            }
        }

        private bool EvilslugIsFriend(MoreSlugcats.SlugNPCAI pup)
        {
            if (pup.friendTracker.friend != null
                && pup.friendTracker.friend is Player slugcat
                && slugcat.slugcatStats.name.value == MOD_ID)
            {
                return true;
            }

            return false;
        }
    }
}