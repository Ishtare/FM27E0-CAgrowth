
// Modified build v1.1.27. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Fm24Editor
{
    partial class MainForm
    {
        static readonly GrowTarget[] EmptyGrowthLimits = new GrowTarget[0];
        GrowTarget[] growthLimitTargets = EmptyGrowthLimits;
        int nextGrowthLimit;

        void RebuildGrowthLimitSchedule()
        {
            growthLimitTargets = growWatch.Values
                .Where(t => !t.Done && t.Focus != null && t.Focus.HasLimits).ToArray();
            nextGrowthLimit = 0;
        }

        void ClearGrowthLimitSchedule()
        {
            growthLimitTargets = EmptyGrowthLimits;
            nextGrowthLimit = 0;
        }

        // Called by the existing growth timer. Checked players share a short time slice.
        void CheckGrowthLimits()
        {
            if (growthLimitTargets.Length == 0 || rosterWorker.Busy || !Safety.On("core")) return;
            var attached = fm;
            if (scannedSaveLoads.HasValue && SaveLoadCount(attached) != scannedSaveLoads)
            {
                StopGrow("载入的存档已变化，请重新挂接");
                return;
            }

            var slice = Stopwatch.StartNew();
            int count = Math.Min(32, growthLimitTargets.Length);
            for (int i = 0; i < count && growOn; i++)
            {
                var target = growthLimitTargets[nextGrowthLimit];
                nextGrowthLimit = (nextGrowthLimit + 1) % growthLimitTargets.Length;
                GrowTarget current;
                if (!target.Done && growWatch.TryGetValue(target.P.Person, out current) && current == target)
                {
                    try { ApplyGrowthLimits(attached, target); }
                    catch (InvalidDataException)
                    {
                        // FM can change the player or calendar between the probe and capture.
                        // Leave the live data alone and recheck with a fresh snapshot.
                    }
                }
                if (slice.ElapsedMilliseconds >= 10) break;
            }
        }

        void ApplyGrowthLimits(FmProcess attached, GrowTarget target)
        {
            var player = target.P;
            var focus = target.Focus;
            // Most polls need only one read. Validate the entire object before any correction.
            var attributes = attached.Read(player.Block + (ulong)FmLayout.AttrStart, FmLayout.AttrCount);
            if (!focus.ExceedsLimits(attributes)) return;

            var frame = GrowthStep.Capture(attached.Read, attached.ReadGameDate, player, PlayerGuards(player));
            if (DayKey(frame.Day) != growDay) return;
            frame.PlanAttributeLimits(focus);
            string error;
            if (!CommitGrowthStep(attached, frame, out error) && error.Contains("重载"))
                StopGrow("属性上限写入后部分数据无法恢复，请重载修改前的存档");
        }
    }
}
