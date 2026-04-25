namespace mcevilslug
{
    public static class EvilEnums
    {
        public static AbstractPhysicalObject.AbstractObjectType PupTrack;
        public static MoreSlugcats.SlugNPCAI.BehaviorType BeingGrabbed;
        public static Player.BodyModeIndex EvilClimbing;
        public static SoundID EvilSniff;
        public static SoundID EvilSniff2;

        public static void RegisterValues()
        {
            PupTrack = new AbstractPhysicalObject.AbstractObjectType("PupTrack", true);
            BeingGrabbed = new MoreSlugcats.SlugNPCAI.BehaviorType("BeingGrabbed", true);
            EvilClimbing = new Player.BodyModeIndex("EvilClimbing", true);
            EvilSniff = new SoundID("EvilSniff", true);
            EvilSniff2 = new SoundID("EvilSniff2", true);
        }

        public static void UnregisterValues()
        {
            AbstractPhysicalObject.AbstractObjectType pupTrack = PupTrack;
            pupTrack?.Unregister();
            PupTrack = null;

            MoreSlugcats.SlugNPCAI.BehaviorType beingGrabbed = BeingGrabbed;
            beingGrabbed?.Unregister();
            BeingGrabbed = null;
            
            SoundID evilSniff = EvilSniff;
            evilSniff?.Unregister();
            EvilSniff = null;
            
            SoundID evilSniff2 = EvilSniff2;
            evilSniff2?.Unregister();
            EvilSniff2 = null;
            
            Player.BodyModeIndex evilClimbing = EvilClimbing;
            evilClimbing?.Unregister();
            EvilClimbing = null;
        }
    }
}
