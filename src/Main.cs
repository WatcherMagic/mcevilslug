using BepInEx;
using mcevilslug;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using On.MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Reflection;
using RWCustom;
using UnityEngine;
using Random = System.Random;

namespace SlugTemplate
{
    [BepInPlugin(MOD_ID, "Evil McEvilslug", "0.1.0")]
    
    [BepInDependency("dynamicpupspawns", BepInDependency.DependencyFlags.HardDependency)]

    class Main : BaseUnityPlugin
    {   
        public const string MOD_ID = "mcevilslug";

        private const int QUARTER_FOOD_AMOUNT_MUSHROOM = 2;
        private const int FOOD_AMOUNT_KARMAFLOWER = 1;

        private const int MAUL_COUNTER = 20;
        private const int SLUG_TO_BACK_COUNTER = 10;
        private int framesPickupHeld = 0;
        private int framesSlugToBackInput = 0;

        private const int PUP_CHUNK_GRAB = 0;
        
        private const float _CANT_SNIFF_SECONDS = 15f; //15 or 20
        private float _sinceLastSniffSeconds = _CANT_SNIFF_SECONDS;
        private const float _SNIFF_HOLD_SECONDS = 1f;
        private float _sniffHeldSeconds = 0f;
        private bool _setNotifySound = false;
        private bool _sniffing = false;
        private const float _SNIFF_DURATION = 1.5f;
        private float _sniffedFor = 0f;
        
        private const float LEAVE_TRACKS_COUNTER = 10f;
        private float leaveTracksTimer = 0f;

        private List<AbstractRoom> realizedShelters = new List<AbstractRoom>();

        internal static BindingFlags bfAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public void OnEnable()
        {
            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);

            //register and unregister enums
            //On.RainWorld.OnModsEnabled += RainWorld_OnModsEnabled;
            EvilEnums.RegisterValues();
            On.RainWorld.OnModsDisabled += RainWorld_OnModsDisabled;

            On.AbstractPhysicalObject.Realize += AbstractPhysicalObject_Realize;

            //world hooks
            //On.AbstractRoom.RealizeRoom += SpawnPupOnShelterRealize;
            //On.World.LoadWorld += ClearList;

            //player hooks
            On.Player.ObjectEaten += AddFood;
            On.Player.GrabUpdate += EvilGrabUpdate;
            On.Player.ThrownSpear += EvilSpearThrow;
            On.Player.Update += EvilPlayerUpdates;
            //On.Player.Jump += LogSoundErrors;
            On.Player.MovementUpdate += EvilClimb;
            On.Player.UpdateBodyMode += TryClimbing;

            //slugnpc hooks
            //SlugNPCAI.Update += PupUpdate;

            try
            {
                IL.Player.Update += NoPopcorn;

            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        private void EvilClimb(On.Player.orig_MovementUpdate orig, Player self, bool eu)
        {
            orig(self, eu);
            
            

            if (self.bodyMode == Player.BodyModeIndex.WallClimb)
            {
                //exit climbing mode
                if (self.input[0].jmp || (self.bodyChunks[0].contactPoint.x == 0 //suspended in air
                                          && self.bodyChunks[1].contactPoint.x == 0
                                          && self.bodyChunks[1].contactPoint.y == 0)
                                      //on ground
                                      || self.bodyChunks[0].contactPoint.y < 0)
                {
                    self.bodyMode = Player.BodyModeIndex.Default;
                    return;
                }
                
                //skid to stop when grabbing wall while falling
                self.bodyChunks[0].vel.y = Mathf.Lerp(self.bodyChunks[0].vel.y, 0f, 0.1f);
            }
        }
        
        private void TryClimbing(On.Player.orig_UpdateBodyMode orig, Player self)
        {
            orig(self);
            
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
                Debug.Log("[evilslug] changed bodymode to wallclimb!");
            }
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

        private void AbstractPhysicalObject_Realize(On.AbstractPhysicalObject.orig_Realize orig, AbstractPhysicalObject self)
        {
            orig(self);
            if (self.type == EvilEnums.PupTrack)
            {
                UnityEngine.Debug.Log("[evilslug] Realizing track " + self.ID);

                self.realizedObject = new Pup_Track(self);
            }
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

            if (self.input[0].pckp && self.grasps[0] != null && self.slugcatStats.name.value == MOD_ID
                && self.grasps[0].grabbed is Creature
                && !(self.grasps[0].grabbed as Creature).dead
                && (self.grasps[0].grabbed as Creature).GetType() == typeof(Player))
            {

                framesPickupHeld += 1;
                if (self.input[1].AnyDirectionalInput && !self.input[0].AnyDirectionalInput)
                {
                    framesSlugToBackInput = 0;
                }
                if (self.input[0].AnyDirectionalInput)
                {
                    framesSlugToBackInput += 1;
                    if (framesSlugToBackInput >= SLUG_TO_BACK_COUNTER)
                    {
                        Logger.LogInfo("Placing pup on back");
                        self.slugOnBack.SlugToBack(self.grasps[0].grabbed as Player);
                        framesSlugToBackInput = 0;
                    }
                }
                else if (framesPickupHeld >= MAUL_COUNTER)
                {
                    //UpdatePupAIState(self.grasps[0].grabbed as Player);
                    (self.grasps[0].grabbed as Player).standing = false;
                    self.Grab(self.grasps[0].grabbed, 0, PUP_CHUNK_GRAB, Creature.Grasp.Shareability.CanNotShare, 0.5f, true, true);
                    self.MaulingUpdate(0);
                }

            }
            if (self.input[1].pckp && !self.input[0].pckp)
            {
                framesPickupHeld = 0;
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

        private void EvilPlayerUpdates(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            _sinceLastSniffSeconds += Time.deltaTime;

            LeaveTrack(self);
            Sniff(self);

            if (_sniffing)
            {
                SniffUpdate(self);
            }
        }
        
        private void LeaveTrack(Player self)
        {
            //spawn track at player location
            if (leaveTracksTimer >= LEAVE_TRACKS_COUNTER
            && (self.isNPC || self.isSlugpup))
            {
                try
                {
                    AbstractPhysicalObject abstractTrack = new(self.room.world, EvilEnums.PupTrack, null,
                    self.room.GetWorldCoordinate(self.bodyChunks[1].pos), self.room.game.GetNewID());
                    Pup_Track track = new Pup_Track(abstractTrack);
                    track.SetPupColor((self.graphicsModule as PlayerGraphics).player.ShortCutColor());
                    track.PlaceInRoom(self.room);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                }

                leaveTracksTimer = 0f;
            }
            leaveTracksTimer += Time.deltaTime;
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
                        }
                        
                        for (int i = 0; i < self.room.physicalObjects.Length; i++)
                        {
                            for (int j = 0; j < self.room.physicalObjects[i].Count; j++)
                            {
                                if (self.room.physicalObjects[i][j] is Pup_Track)
                                {
                                    (self.room.physicalObjects[i][j] as Pup_Track).SetVisibleTrue();
                                }
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

        // private void UpdatePupAIState(Player npc)
        // {
        //     npc.AI.behaviorType = EvilEnums.BeingGrabbed;
        // }

        // private void PupUpdate(SlugNPCAI.orig_Update orig, MoreSlugcats.SlugNPCAI self)
        // {
        //     orig(self);
        //
        //     if (self.behaviorType == EvilEnums.BeingGrabbed)
        //     {
        //         UnityEngine.Debug.Log("[evilslug] Pup is being grabbed");
        //     }
        // }
    }
}