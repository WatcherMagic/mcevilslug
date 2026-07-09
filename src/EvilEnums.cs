namespace mcevilslug
{
    public static class EvilEnums
    {
        public static AbstractPhysicalObject.AbstractObjectType PupTrack;
        public static MoreSlugcats.SlugNPCAI.BehaviorType BeingGrabbed;
        public static Player.BodyModeIndex EvilClimbing;
        public static SoundID EvilSniff;

        public static void RegisterValues()
        {
            PupTrack = new AbstractPhysicalObject.AbstractObjectType("PupTrack", true);
            //BeingGrabbed = new MoreSlugcats.SlugNPCAI.BehaviorType("BeingGrabbed", true);
            EvilClimbing = new Player.BodyModeIndex("EvilClimbing", true);
            EvilSniff = new SoundID("EvilSniff", true);
        }

        public static void UnregisterValues()
        {
            AbstractPhysicalObject.AbstractObjectType pupTrack = PupTrack;
            pupTrack?.Unregister();
            PupTrack = null;

            // MoreSlugcats.SlugNPCAI.BehaviorType beingGrabbed = BeingGrabbed;
            // beingGrabbed?.Unregister();
            // BeingGrabbed = null;
            
            Player.BodyModeIndex evilClimbing = EvilClimbing;
            evilClimbing?.Unregister();
            EvilClimbing = null;
            
            SoundID evilSniff = EvilSniff;
            evilSniff?.Unregister();
            EvilClimbing = null;
        }
    }
}
