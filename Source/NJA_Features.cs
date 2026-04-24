using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace NoJobAuthors
{
    internal static class NJA_Features
    {
        private static readonly string[] FinishItIds = { "xandrmoro.rim.finishit", "Xandrmoro.Rim.FinishIt" };
        private static readonly string[] LifeLessonsIds = { "ghostdata.lifelessons", "GhostData.lifelessons" };
        private static readonly string[] AchtungIds = { "brrainz.achtung" };
        private static readonly string[] VpeIds = { "vanillaexpanded.vpsycastse", "VanillaExpanded.VPsycastsE" };

        internal static bool FinishItActive => IsAnyModActive(FinishItIds);
        internal static bool LifeLessonsActive => IsAnyModActive(LifeLessonsIds);
        internal static bool AchtungActive => IsAnyModActive(AchtungIds);
        internal static bool VpeActive => IsAnyModActive(VpeIds);

        private static readonly MethodInfo FinishUftJobMethod =
            AccessTools.Method(typeof(WorkGiver_DoBill), "FinishUftJob");

        private static PropertyInfo _achtungForcedWorkInstanceProperty;
        private static MethodInfo _achtungAllForcedJobsMethod;
        private static FieldInfo _achtungForcedJobPawnField;
        private static FieldInfo _achtungForcedJobTargetsField;
        private static FieldInfo _achtungForcedTargetItemField;
        private static bool _achtungReflectionInitialized;

        private static void EnsureAchtungReflection()
        {
            if (_achtungReflectionInitialized)
                return;
            _achtungReflectionInitialized = true;
            _achtungForcedWorkInstanceProperty = AccessTools.Property("AchtungMod.ForcedWork:Instance");
            _achtungAllForcedJobsMethod = AccessTools.Method("AchtungMod.ForcedWork:AllForcedJobs");
            _achtungForcedJobPawnField = AccessTools.Field("AchtungMod.ForcedJob:pawn");
            _achtungForcedJobTargetsField = AccessTools.Field("AchtungMod.ForcedJob:targets");
            _achtungForcedTargetItemField = AccessTools.Field("AchtungMod.ForcedTarget:item");
        }

        internal static bool ShouldUseSharedAuthoring(RecipeDef recipe)
        {
            if (recipe == null)
                return true;

            if (Mod_NoJobAuthors.Settings?.onlyApplyToNonQualityItems == true && IsQualitySensitiveRecipe(recipe))
            {
                NJA_Logging.DebugThrottled(
                    $"quality.skip.{recipe.defName}",
                    $"Skipping shared-author behavior for quality-sensitive recipe '{recipe.defName}'.",
                    600);
                return false;
            }

            return true;
        }

        internal static string EveryoneLabel()
        {
            return "NJA_Everyone".Translate().Resolve();
        }

        internal static void LogStartupDiagnostics(string packageId)
        {
            NJA_Logging.DebugOnce("startup.diagnostics", 
                $"Startup diagnostics: packageId={packageId}, " +
                $"forceFinishUnfinishedFirst={Mod_NoJobAuthors.Settings?.forceFinishUnfinishedFirst ?? false}, " +
                $"enableFinishItCompat={Mod_NoJobAuthors.Settings?.enableFinishItCompat ?? false}, " +
                $"enableAchtungCompat={Mod_NoJobAuthors.Settings?.enableAchtungCompat ?? false}, " +
                $"enableLifeLessonsCompat={Mod_NoJobAuthors.Settings?.enableLifeLessonsCompat ?? false}, " +
                $"enableVpeCompat={Mod_NoJobAuthors.Settings?.enableVpeCompat ?? false}, " +
                $"onlyApplyToNonQualityItems={Mod_NoJobAuthors.Settings?.onlyApplyToNonQualityItems ?? false}, " +
                $"preventUnfinishedInStockpiles={Mod_NoJobAuthors.Settings?.preventUnfinishedInStockpiles ?? false}, " +
                $"FinishItActive={FinishItActive}, LifeLessonsActive={LifeLessonsActive}, AchtungActive={AchtungActive}, VpeActive={VpeActive}");
        }

        internal static bool IsClaimedByForeignAchtungForcedJob(Pawn pawn, Thing target)
        {
            if (Mod_NoJobAuthors.Settings?.enableAchtungCompat != true || !AchtungActive || pawn == null || target == null)
                return false;

            try
            {
                EnsureAchtungReflection();
                object forcedWork = _achtungForcedWorkInstanceProperty?.GetValue(null);
                if (forcedWork == null || _achtungAllForcedJobsMethod == null)
                    return false;

                if (!(_achtungAllForcedJobsMethod.Invoke(forcedWork, null) is System.Collections.IEnumerable forcedJobs))
                    return false;

                foreach (object forcedJob in forcedJobs)
                {
                    Pawn forcedPawn = _achtungForcedJobPawnField?.GetValue(forcedJob) as Pawn;
                    if (forcedPawn == null || forcedPawn == pawn)
                        continue;

                    if (!(_achtungForcedJobTargetsField?.GetValue(forcedJob) is System.Collections.IEnumerable forcedTargets))
                        continue;

                    foreach (object forcedTarget in forcedTargets)
                    {
                        if (!(_achtungForcedTargetItemField?.GetValue(forcedTarget) is LocalTargetInfo item))
                            continue;

                        if (item.HasThing && item.Thing == target)
                        {
                            NJA_Logging.DebugThrottled(
                                $"achtung.claimed.{target.thingIDNumber}.{forcedPawn.ThingID}",
                                $"Achtung compat skipped unfinished target '{target.LabelCap}' because it is already forced for {forcedPawn.LabelShort}.",
                                180);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NJA_Logging.Warn($"Achtung compat inspection failed: {ex.Message}");
            }

            return false;
        }

        internal static bool TryTakeFinishItJob(UnfinishedThing unfinishedThing)
        {
            if (Mod_NoJobAuthors.Settings?.enableFinishItCompat != true || !FinishItActive)
                return false;

            if (!(unfinishedThing?.BoundBill is Bill_ProductionWithUft bill))
                return false;

            Thing boundWorkTable = unfinishedThing.BoundWorkTable;
            if (!(boundWorkTable is Building_WorkTable workTable))
                return false;

            WorkGiver_DoBill worker = BillUtility.GetWorkgiver(workTable)?.Worker as WorkGiver_DoBill;
            if (worker == null)
            {
                NJA_Logging.Warn($"Finish It compat could not resolve a WorkGiver_DoBill for '{unfinishedThing.LabelCap}'.");
                return false;
            }

            foreach (Pawn pawn in FinishItCandidates(unfinishedThing, bill))
            {
                Job job = CreateFinishUftJob(pawn, unfinishedThing, bill);
                if (job == null)
                    continue;

                NJA_Logging.Debug($"Finish It compat assigned '{unfinishedThing.LabelCap}' to {pawn.LabelShort}.");
                pawn.jobs.TryTakeOrderedJobPrioritizedWork(job, worker, unfinishedThing.Position);
                return true;
            }

            NJA_Logging.Warn($"Finish It compat found no eligible pawn for '{unfinishedThing.LabelCap}'.");
            return false;
        }

        internal static int RemoveUnfinishedFromStockpileFilters()
        {
            if (Current.Game == null)
                return 0;

            int updatedFilters = 0;
            List<ThingDef> unfinishedDefs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def?.thingClass != null && typeof(UnfinishedThing).IsAssignableFrom(def.thingClass))
                .ToList();

            foreach (Map map in Find.Maps)
            {
                foreach (Building_Storage storage in map.listerBuildings.AllBuildingsColonistOfClass<Building_Storage>())
                {
                    StorageSettings settings = storage.GetStoreSettings();
                    if (settings?.filter == null)
                        continue;

                    bool changed = false;
                    foreach (ThingDef def in unfinishedDefs)
                    {
                        if (!settings.filter.Allows(def))
                            continue;

                        settings.filter.SetAllow(def, false);
                        changed = true;
                    }

                    if (changed)
                    {
                        updatedFilters++;
                        NJA_Logging.Debug($"Removed unfinished-item allowances from storage '{storage.LabelCap}'.");
                    }
                }
            }

            return updatedFilters;
        }

        internal static bool ShouldRejectStorage(Thing thing)
        {
            if (Mod_NoJobAuthors.Settings?.preventUnfinishedInStockpiles != true)
                return false;

            return thing is UnfinishedThing;
        }

        private static Job CreateFinishUftJob(Pawn pawn, UnfinishedThing unfinishedThing, Bill_ProductionWithUft bill)
        {
            if (pawn == null || unfinishedThing == null || bill == null || FinishUftJobMethod == null)
                return null;

            try
            {
                return FinishUftJobMethod.Invoke(null, new object[] { pawn, unfinishedThing, bill }) as Job;
            }
            catch (Exception ex)
            {
                NJA_Logging.Warn($"Finish It compat failed to create a finish-UFT job: {ex.Message}");
                return null;
            }
        }

        private static IEnumerable<Pawn> FinishItCandidates(UnfinishedThing unfinishedThing, Bill_ProductionWithUft bill)
        {
            HashSet<Pawn> seen = new HashSet<Pawn>();

            if (bill.BoundWorker != null && FinishItCandidateAllowed(bill.BoundWorker, unfinishedThing) && seen.Add(bill.BoundWorker))
                yield return bill.BoundWorker;

            foreach (Pawn pawn in unfinishedThing.MapHeld?.mapPawns?.FreeColonistsSpawned ?? Enumerable.Empty<Pawn>())
            {
                if (!seen.Add(pawn))
                    continue;

                if (FinishItCandidateAllowed(pawn, unfinishedThing))
                    yield return pawn;
            }
        }

        private static bool FinishItCandidateAllowed(Pawn pawn, UnfinishedThing unfinishedThing)
        {
            if (pawn?.jobs == null || unfinishedThing == null)
                return false;

            try
            {
                return !pawn.Dead &&
                       !pawn.Downed &&
                       pawn.Spawned &&
                       unfinishedThing.Spawned &&
                       !unfinishedThing.IsForbidden(pawn) &&
                       pawn.CanReserveAndReach(unfinishedThing, PathEndMode.InteractionCell, pawn.NormalMaxDanger());
            }
            catch (Exception ex)
            {
                NJA_Logging.Warn($"Finish It compat candidate check failed for {pawn?.LabelShort ?? "null"}: {ex.Message}");
                return false;
            }
        }

        private static bool IsAnyModActive(IEnumerable<string> packageIds)
        {
            try
            {
                HashSet<string> ids = new HashSet<string>(packageIds, StringComparer.OrdinalIgnoreCase);
                return LoadedModManager.RunningModsListForReading.Any(mod =>
                    ids.Contains(mod.PackageIdPlayerFacing) ||
                    ids.Contains(mod.PackageIdPlayerFacing?.ToLowerInvariant()));
            }
            catch (Exception ex)
            {
                NJA_Logging.Warn($"Failed to resolve mod activity: {ex.Message}");
                return false;
            }
        }

        private static bool IsQualitySensitiveRecipe(RecipeDef recipe)
        {
            if (recipe?.products == null)
                return false;

            foreach (ThingDefCountClass product in recipe.products)
            {
                ThingDef thingDef = product?.thingDef;
                if (thingDef?.comps == null)
                    continue;

                if (thingDef.comps.Any(comp => comp?.compClass != null && typeof(CompQuality).IsAssignableFrom(comp.compClass)))
                    return true;
            }

            return false;
        }
    }
}
