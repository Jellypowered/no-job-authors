using Verse;

namespace NoJobAuthors
{
    public class NoJobAuthorsSettings : ModSettings
    {
        public bool forceFinishUnfinishedFirst = false;
        public bool enableFinishItCompat = false;
        public bool enableAchtungCompat = false;
        public bool enableLifeLessonsCompat = false;
        public bool enableVpeCompat = false;
        public bool onlyApplyToNonQualityItems = false;
        public bool preventUnfinishedInStockpiles = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref forceFinishUnfinishedFirst, "forceFinishUnfinishedFirst", false);
            Scribe_Values.Look(ref enableFinishItCompat, "enableFinishItCompat", false);
            Scribe_Values.Look(ref enableAchtungCompat, "enableAchtungCompat", false);
            Scribe_Values.Look(ref enableLifeLessonsCompat, "enableLifeLessonsCompat", false);
            Scribe_Values.Look(ref enableVpeCompat, "enableVpeCompat", false);
            Scribe_Values.Look(ref onlyApplyToNonQualityItems, "onlyApplyToNonQualityItems", false);
            Scribe_Values.Look(ref preventUnfinishedInStockpiles, "preventUnfinishedInStockpiles", false);
        }
    }
}
