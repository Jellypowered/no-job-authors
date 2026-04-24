using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using Verse.AI;
using HarmonyLib;
using RimWorld.Planet;

namespace NoJobAuthors
{
    public class Mod_NoJobAuthors : Mod
    {
        public static NoJobAuthorsSettings Settings;

        public Mod_NoJobAuthors(ModContentPack content) : base(content)
        {
            Settings = GetSettings<NoJobAuthorsSettings>();
            new Harmony(this.Content.PackageIdPlayerFacing).PatchAll();
            NJA_Logging.DebugOnce("startup.patchall", $"Patched with package id '{this.Content.PackageIdPlayerFacing}'.");
            NJA_Features.LogStartupDiagnostics(this.Content.PackageIdPlayerFacing);
        }

        public override string SettingsCategory() => "No Job Authors";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "Force-finish unfinished items before starting new crafts",
                ref Settings.forceFinishUnfinishedFirst,
                "When enabled, pawns will finish any existing unfinished item matching a recipe before starting a brand-new craft of that type."
            );
            listing.GapLine();
            listing.Label("Beta Features");
            listing.CheckboxLabeled(
                "Enable Finish It! compatibility",
                ref Settings.enableFinishItCompat,
                "When enabled, the Finish It! gizmo is rerouted through a compatibility shim so it can still assign a pawn even though No Job Authors suppresses creator ownership."
            );
            listing.CheckboxLabeled(
                "Enable Achtung! compatibility",
                ref Settings.enableAchtungCompat,
                "When enabled, unfinished items already targeted by another pawn's Achtung! forced job are skipped to reduce forced-job stealing."
            );
            listing.CheckboxLabeled(
                "Enable Life Lessons compatibility diagnostics",
                ref Settings.enableLifeLessonsCompat,
                "When enabled, debug builds log Life Lessons proficiency checks on unfinished-bill paths."
            );
            listing.CheckboxLabeled(
                "Enable VPE compatibility diagnostics",
                ref Settings.enableVpeCompat,
                "When enabled, debug builds log VPE Craft Timeskip interactions with unfinished items."
            );
            listing.CheckboxLabeled(
                "Only apply to non-quality items",
                ref Settings.onlyApplyToNonQualityItems,
                "When enabled, quality-sensitive recipes keep vanilla author/worker behavior."
            );
            listing.CheckboxLabeled(
                "Prevent unfinished items from entering stockpiles",
                ref Settings.preventUnfinishedInStockpiles,
                "When enabled, storage buildings reject unfinished items."
            );
            if (listing.ButtonText("Remove unfinished items from current stockpile filters"))
            {
                int updatedFilters = NJA_Features.RemoveUnfinishedFromStockpileFilters();
                Messages.Message($"Updated {updatedFilters} stockpile filters.", MessageTypeDefOf.TaskCompletion, false);
            }
            listing.End();
        }
        
        [HarmonyPatch(typeof(WorkGiver_DoBill), "ClosestUnfinishedThingForBill")]
        public static class WorkGiver_DoBill_ClosestUnfinishedThingForBill_Patch
        {
            [HarmonyPrefix]
            public static bool ClosestUnfinishedThingForBill(ref UnfinishedThing __result, Pawn pawn, Bill_ProductionWithUft bill)
            {
                if (!NJA_Features.ShouldUseSharedAuthoring(bill?.recipe))
                    return true;

                string pawnId = pawn?.ThingID ?? "null";
                string recipeName = bill?.recipe?.defName ?? "null";
                NJA_Logging.DebugThrottled($"closest.search.{pawnId}.{recipeName}", $"ClosestUnfinishedThingForBill search pawn={pawnId} recipe={recipeName}");

                bool Validator(Thing t) => !t.IsForbidden(pawn) &&
                                           !NJA_Features.IsClaimedByForeignAchtungForcedJob(pawn, t) &&
                                           ((UnfinishedThing)t).Recipe == bill.recipe &&
                                           ((UnfinishedThing)t).ingredients.TrueForAll(x => bill.IsFixedOrAllowedIngredient(x.def)) &&
                                           pawn.CanReserve(t);

                ThingRequest thingReq = ThingRequest.ForDef(bill.recipe.unfinishedThingDef);
                TraverseParms traverseParams = TraverseParms.For(pawn, pawn.NormalMaxDanger());

                __result = (UnfinishedThing)GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, thingReq, PathEndMode.InteractionCell, traverseParams, validator: Validator);
                if (__result != null)
                    NJA_Logging.DebugThrottled($"closest.found.{pawnId}.{recipeName}", $"Found unfinished thing id={__result.thingIDNumber} for recipe={recipeName}");
                else
                    NJA_Logging.DebugThrottled($"closest.none.{pawnId}.{recipeName}", $"No unfinished thing found for recipe={recipeName}");
                return false;
            }
        }

        [HarmonyPatch(typeof(UnfinishedThing), "get_Creator")]
        public static class UnfinishedThing_GetCreator_Patch
        {
            [HarmonyPrefix]
            public static bool Creator(UnfinishedThing __instance, ref Pawn __result)
            {
                if (!NJA_Features.ShouldUseSharedAuthoring(__instance?.Recipe))
                    return true;

                __result = null;
                NJA_Logging.DebugThrottled("creator.get", "UnfinishedThing.Creator getter forced to null.", 600);
                return false;
            }
        }

        [HarmonyPatch(typeof(UnfinishedThing), "set_Creator")]
        public static class UnfinishedThing_SetCreator_Patch
        {
            private static readonly AccessTools.FieldRef<UnfinishedThing, string> _creatorName = AccessTools.FieldRefAccess<UnfinishedThing, string>("creatorName");
            [HarmonyPostfix]
            public static void Creator(UnfinishedThing __instance)
            {
                if (!NJA_Features.ShouldUseSharedAuthoring(__instance?.Recipe))
                    return;

                _creatorName(__instance) = NJA_Features.EveryoneLabel();
                NJA_Logging.DebugThrottled($"creator.set.{__instance?.thingIDNumber ?? -1}", $"UnfinishedThing.Creator setter normalized to '{NJA_Features.EveryoneLabel()}'.", 600);
            }
        }

        [HarmonyPatch(typeof(WorkGiver_DoBill), "StartOrResumeBillJob")]
        public static class WorkGiver_DoBill_StartOrResumeBillJob_Patch
        {
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> StartOrResumeBillJob(IEnumerable<CodeInstruction> instructions)
            {
                var arr = instructions.ToArray();
                int rewrites = 0;
                for (var index = 0; index < arr.Length; index++)
                {
                    if (arr[index + 0].opcode == OpCodes.Ldloc_S &&
                        arr[index + 1].opcode == OpCodes.Callvirt &&
                        arr[index + 2].opcode == OpCodes.Ldarg_1 &&
                        arr[index + 3].opcode == OpCodes.Bne_Un)

                    {
                        yield return new CodeInstruction(OpCodes.Nop);
                        yield return new CodeInstruction(OpCodes.Nop);
                        yield return new CodeInstruction(OpCodes.Nop);
                        yield return new CodeInstruction(OpCodes.Nop);
                        rewrites++;
                        index += 3;
                    }
                    else
                        yield return arr[index];
                }

                NJA_Logging.DebugOnce("transpile.startorresume", $"StartOrResumeBillJob transpiler rewrites={rewrites}.");
            }
        }


        [HarmonyPatch(typeof(WorkGiver_DoBill), "FinishUftJob")]
        public static class WorkGiver_DoBill_FinishUftJob_Patch
        {
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> FinishUftJob(IEnumerable<CodeInstruction> instructions)
            {
                var arr = instructions.ToArray();
                int rewrites = 0;
                for (var index = 0; index < arr.Length; index++)
                {
                    if (arr[index + 0].opcode == OpCodes.Ldarg_1 &&
                        arr[index + 1].opcode == OpCodes.Callvirt &&
                        arr[index + 2].opcode == OpCodes.Ldarg_0 &&
                        arr[index + 3].opcode == OpCodes.Beq_S)
                    {
                        yield return new CodeInstruction(OpCodes.Nop);
                        yield return new CodeInstruction(OpCodes.Nop);
                        yield return new CodeInstruction(OpCodes.Nop);
                        yield return new CodeInstruction(OpCodes.Br, arr[index + 3].operand);
                        rewrites++;
                        index += 3;
                    }
                    else
                        yield return arr[index];
                }

                NJA_Logging.DebugOnce("transpile.finishuft", $"FinishUftJob transpiler rewrites={rewrites}.");
            }
        }
        [HarmonyPatch(typeof(WorkGiver_DoBill), "StartOrResumeBillJob")]
        public static class WorkGiver_DoBill_UnfinishedFirst_Patch
        {
            private static readonly MethodInfo _finishUftJob =
                AccessTools.Method(typeof(WorkGiver_DoBill), "FinishUftJob");

            [HarmonyPrefix]
            [HarmonyPriority(Priority.High)]
            public static bool UnfinishedFirst(ref Job __result, Pawn pawn, IBillGiver giver, WorkGiver_DoBill __instance)
            {
                if (Mod_NoJobAuthors.Settings == null || !Mod_NoJobAuthors.Settings.forceFinishUnfinishedFirst)
                    return true;

                NJA_Logging.DebugThrottled($"uft.first.scan.{pawn?.ThingID ?? "null"}",
                    $"UnfinishedFirst: scanning bills for {pawn?.LabelShort ?? "null"}");

                foreach (var bill in giver.BillStack)
                {
                    if (!(bill is Bill_ProductionWithUft billUft)) continue;
                    if (billUft.recipe?.unfinishedThingDef == null) continue;
                    if (!NJA_Features.ShouldUseSharedAuthoring(billUft.recipe)) continue;
                    if (!billUft.ShouldDoNow()) continue;

                    var uft = FindClosestUft(pawn, billUft);
                    if (uft == null) continue;

                    NJA_Logging.Debug(
                        $"UnfinishedFirst: directing {pawn.LabelShort} to finish UFT #{uft.thingIDNumber} recipe={billUft.recipe.defName}");

                    var job = (Job)_finishUftJob?.Invoke(__instance, new object[] { pawn, uft, billUft });
                    if (job != null)
                    {
                        __result = job;
                        return false;
                    }
                }

                NJA_Logging.DebugThrottled($"uft.first.none.{pawn?.ThingID ?? "null"}",
                    $"UnfinishedFirst: no eligible UFT found for {pawn?.LabelShort ?? "null"}, proceeding normally.");
                return true;
            }

            private static UnfinishedThing FindClosestUft(Pawn pawn, Bill_ProductionWithUft bill)
            {
                if (!NJA_Features.ShouldUseSharedAuthoring(bill?.recipe)) return null;
                if (bill.recipe?.unfinishedThingDef == null) return null;

                ThingRequest thingReq = ThingRequest.ForDef(bill.recipe.unfinishedThingDef);
                TraverseParms traverseParams = TraverseParms.For(pawn, pawn.NormalMaxDanger());

                return (UnfinishedThing)GenClosest.ClosestThingReachable(
                    pawn.Position, pawn.Map, thingReq,
                    PathEndMode.InteractionCell, traverseParams,
                    validator: t => !t.IsForbidden(pawn) &&
                                   !NJA_Features.IsClaimedByForeignAchtungForcedJob(pawn, t) &&
                                   ((UnfinishedThing)t).Recipe == bill.recipe &&
                                   ((UnfinishedThing)t).ingredients.TrueForAll(x => bill.IsFixedOrAllowedIngredient(x.def)) &&
                                   pawn.CanReserve(t));
            }
        }

        [HarmonyPatch(typeof(Building_Storage), "Accepts")]
        public static class Building_Storage_Accepts_Patch
        {
            [HarmonyPostfix]
            public static void Accepts(Building_Storage __instance, Thing t, ref bool __result)
            {
                if (!__result)
                    return;

                if (!NJA_Features.ShouldRejectStorage(t))
                    return;

                __result = false;
                NJA_Logging.DebugThrottled(
                    $"storage.reject.{__instance?.thingIDNumber ?? -1}.{t?.thingIDNumber ?? -1}",
                    $"Storage '{__instance?.LabelCap ?? "unknown"}' rejected unfinished item '{t?.LabelCap ?? "null"}'.",
                    240);
            }
        }

        [HarmonyPatch]
        public static class FinishIt_UnfinishedThing_GetGizmos_CompatPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method("FinishIt.UnfinishedThing_GetGizmos_Patch:Postfix");
            }

            private static bool Prepare()
            {
                return AccessTools.TypeByName("FinishIt.UnfinishedThing_GetGizmos_Patch") != null;
            }

            [HarmonyPostfix]
            public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, UnfinishedThing __instance)
            {
                foreach (Gizmo gizmo in __result)
                {
                    if (!(gizmo is Command_Action action) ||
                        Mod_NoJobAuthors.Settings?.enableFinishItCompat != true ||
                        !NJA_Features.FinishItActive ||
                        ((Command)action).defaultLabel != "Finish it!")
                    {
                        yield return gizmo;
                        continue;
                    }

                    Action originalAction = action.action;
                    action.action = delegate
                    {
                        if (!NJA_Features.TryTakeFinishItJob(__instance))
                        {
                            NJA_Logging.Warn($"Finish It compat falling back to original gizmo action for '{__instance?.LabelCap ?? "null"}'.");
                            originalAction?.Invoke();
                        }
                    };

                    NJA_Logging.DebugOnce("finishit.gizmo.wrap", "Installed Finish It! gizmo compatibility wrapper.");
                    yield return gizmo;
                }
            }
        }

        [HarmonyPatch]
        public static class LifeLessons_DoBill_StartOrResumeBillJob_CompatPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method("LifeLessons.Patches.DoBill_StartOrResumeBillJob_Patch:CheckProficiency");
            }

            private static bool Prepare()
            {
                return AccessTools.TypeByName("LifeLessons.Patches.DoBill_StartOrResumeBillJob_Patch") != null;
            }

            [HarmonyPostfix]
            public static void Postfix(Pawn pawn, Bill bill, bool __result)
            {
                if (Mod_NoJobAuthors.Settings?.enableLifeLessonsCompat != true || !NJA_Features.LifeLessonsActive)
                    return;

                if (!(bill is Bill_ProductionWithUft) || bill.recipe?.UsesUnfinishedThing != true)
                    return;

                NJA_Logging.DebugThrottled(
                    $"lifelessons.check.{pawn?.ThingID ?? "null"}.{bill.recipe.defName}",
                    $"Life Lessons proficiency check pawn={pawn?.LabelShort ?? "null"} recipe={bill.recipe.defName} allowed={__result}",
                    180);
            }
        }

        [HarmonyPatch]
        public static class Vpe_Ability_Finish_CompatPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method("VanillaPsycastsExpanded.Chronopath.Ability_Finish:Cast");
            }

            private static bool Prepare()
            {
                return AccessTools.TypeByName("VanillaPsycastsExpanded.Chronopath.Ability_Finish") != null;
            }

            [HarmonyPrefix]
            public static void Prefix(GlobalTargetInfo[] targets)
            {
                if (Mod_NoJobAuthors.Settings?.enableVpeCompat != true || !NJA_Features.VpeActive || targets == null)
                    return;

                foreach (GlobalTargetInfo target in targets)
                {
                    if (!(target.Thing is UnfinishedThing unfinishedThing))
                        continue;

                    NJA_Logging.Debug(
                        $"VPE Finish prefix: target={unfinishedThing.LabelCap} creatorNull={unfinishedThing.Creator == null} boundBill={(unfinishedThing.BoundBill != null)} recipe={unfinishedThing.Recipe?.defName ?? "null"}");
                }
            }

            [HarmonyPostfix]
            public static void Postfix(GlobalTargetInfo[] targets)
            {
                if (Mod_NoJobAuthors.Settings?.enableVpeCompat != true || !NJA_Features.VpeActive || targets == null)
                    return;

                foreach (GlobalTargetInfo target in targets)
                {
                    if (target.Thing is UnfinishedThing unfinishedThing)
                    {
                        NJA_Logging.Debug(
                            $"VPE Finish postfix: unfinished target still exists={ !unfinishedThing.Destroyed } boundBill={(unfinishedThing.BoundBill != null)} recipe={unfinishedThing.Recipe?.defName ?? "null"}");
                        continue;
                    }

                    NJA_Logging.Debug("VPE Finish postfix: target no longer an unfinished thing after cast.");
                }
            }
        }

        [HarmonyPatch(typeof(Bill_ProductionWithUft), "get_BoundWorker")]
        internal class Patch_Bill_ProductionWithUft
        {
            private static readonly AccessTools.FieldRef<Bill_ProductionWithUft, UnfinishedThing> _boundUft = AccessTools.FieldRefAccess<Bill_ProductionWithUft, UnfinishedThing>("boundUftInt");
            private static readonly AccessTools.FieldRef<UnfinishedThing, Pawn> _creator = AccessTools.FieldRefAccess<UnfinishedThing, Pawn>("creatorInt");

            [HarmonyAfter("Harmony_PrisonLabor")]
            [HarmonyPrefix]
            private static bool Prefix(Bill_ProductionWithUft __instance, ref Pawn __result)
            {
                if (!NJA_Features.ShouldUseSharedAuthoring(__instance?.recipe))
                    return true;

                UnfinishedThing uft = _boundUft(__instance);
                if (uft == null)
                {
                    __result = null;
                    return false;
                }

                Pawn creator = _creator(uft);
                if (creator == null || creator.Downed || creator.HostFaction != null || creator.Destroyed || !creator.Spawned)
                {
                    __result = null;
                    NJA_Logging.DebugThrottled(
                        $"boundworker.invalid.{uft.thingIDNumber}",
                        $"BoundWorker override kept bound UFT #{uft.thingIDNumber} attached while creator was unavailable.",
                        240);
                    return false;
                }

                if (__instance.billStack?.billGiver is Thing thing)
                {
                    WorkTypeDef workTypeDef = null;
                    List<WorkGiverDef> allDefsListForReading = DefDatabase<WorkGiverDef>.AllDefsListForReading;
                    for (int i = 0; i < allDefsListForReading.Count; i++)
                    {
                        if (allDefsListForReading[i].fixedBillGiverDefs != null && allDefsListForReading[i].fixedBillGiverDefs.Contains(thing.def))
                        {
                            workTypeDef = allDefsListForReading[i].workType;
                            break;
                        }
                    }

                    if (workTypeDef != null && creator.workSettings != null && !creator.workSettings.WorkIsActive(workTypeDef))
                    {
                        __result = null;
                        NJA_Logging.DebugThrottled(
                            $"boundworker.disabled.{creator.ThingID}.{__instance.recipe?.defName ?? "null"}",
                            $"BoundWorker override returned null because {creator.LabelShort} has work type '{workTypeDef.defName}' disabled.",
                            240);
                        return false;
                    }
                }

                __result = creator;
                NJA_Logging.DebugThrottled(
                    $"boundworker.prefix.{creator.ThingID}.{__instance.recipe?.defName ?? "null"}",
                    $"BoundWorker override returned raw creator {creator.LabelShort} for recipe '{__instance.recipe?.defName ?? "null"}'.",
                    240);
                return false;
            }

        }
    }     
}