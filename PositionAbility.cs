// Modified build v1.1.25. See third-party/FM27-Scout-LICENSE.txt and docs/attribute-optimizer.md.
using System;
using System.IO;
using System.Linq;

namespace Fm24Editor
{
    // Rows follow FmLayout.Attrs. Columns are GK, FB, CB, WB, DM, WM, CM, W, AM, ST.
    // Computation uses the 1–100 attribute bytes, including locked attributes.
    sealed class PositionAbility
    {
        static readonly byte[,] Weights = {
            {0,2,1,3,1,5,1,5,1,2}, // crossing
            {0,1,1,2,2,3,2,5,3,5}, // dribbling
            {0,1,1,1,2,2,2,2,3,8}, // finishing
            {1,2,5,1,1,1,1,1,1,6}, // heading
            {0,1,1,1,3,2,3,2,3,2}, // long_shots
            {0,3,8,2,3,1,3,1,1,1}, // marking
            {0,1,1,2,1,2,3,2,3,6}, // off_the_ball
            {3,2,2,3,4,3,6,2,4,2}, // passing
            {0,1,1,1,1,1,1,1,1,1}, // penalty_taking
            {0,4,5,3,7,2,3,2,2,1}, // tackling
            {1,2,1,2,4,3,6,3,6,2}, // vision
            {8,0,0,0,0,0,0,0,0,0}, // handling
            {6,0,0,0,0,0,0,0,0,0}, // aerial_reach
            {6,0,0,0,0,0,0,0,0,0}, // command_of_area
            {5,0,0,0,0,0,0,0,0,0}, // communication
            {5,0,0,0,0,0,0,0,0,0}, // kicking
            {3,0,0,0,0,0,0,0,0,0}, // throwing
            {3,3,5,3,5,3,3,3,3,5}, // anticipation
            {10,7,10,5,8,5,7,5,6,5}, // decisions
            {4,0,0,0,0,0,0,0,0,0}, // one_on_ones
            {5,4,8,3,5,1,3,1,2,2}, // positioning
            {8,0,0,0,0,0,0,0,0,0}, // reflexes
            {1,3,2,3,4,4,6,5,5,6}, // first_touch
            {1,2,1,3,3,4,4,4,5,4}, // technique
            {0,0,0,0,0,0,0,0,0,0}, // left_foot
            {0,0,0,0,0,0,0,0,0,0}, // right_foot
            {0,0,0,0,0,0,0,0,0,0}, // flair
            {0,1,1,1,1,1,1,1,1,1}, // corners
            {2,2,1,2,2,2,2,2,2,1}, // teamwork
            {1,2,2,2,4,3,3,3,3,2}, // work_rate
            {0,1,1,1,1,1,1,1,1,1}, // long_throws
            {0,0,0,0,0,0,0,0,0,0}, // eccentricity
            {0,0,0,0,0,0,0,0,0,0}, // rushing_out
            {0,0,0,0,0,0,0,0,0,0}, // punching
            {6,7,6,8,6,8,6,10,9,10}, // acceleration
            {0,1,1,1,1,1,1,1,1,1}, // free_kick_taking
            {4,4,6,4,5,3,4,3,3,6}, // strength
            {1,6,3,7,4,5,6,7,6,6}, // stamina
            {3,5,5,6,4,6,5,10,7,7}, // pace
            {1,2,6,1,1,1,1,1,1,5}, // jumping_reach
            {2,1,2,1,1,1,1,1,1,1}, // leadership
            {0,0,0,0,0,0,0,0,0,0}, // dirtiness
            {2,2,2,2,2,2,2,2,2,2}, // balance
            {6,2,2,1,1,1,1,1,1,1}, // bravery
            {0,0,0,0,0,0,0,0,0,0}, // consistency
            {0,0,0,0,0,0,0,0,0,0}, // aggression
            {8,6,6,5,6,6,6,6,6,6}, // agility
            {0,0,0,0,0,0,0,0,0,0}, // important_matches
            {0,0,0,0,0,0,0,0,0,0}, // injury_proneness
            {0,0,0,0,0,0,0,0,0,0}, // versatility
            {0,0,0,0,0,0,0,0,0,0}, // natural_fitness
            {0,0,0,0,0,0,0,0,0,0}, // determination
            {2,2,2,2,2,3,3,3,3,6}, // composure
            {6,4,4,3,3,2,2,2,2,2}, // concentration
        };
        static readonly double[] Feet = {3,4,4.5,4,5,5,6,5.5,7,7.5};
        static readonly int[] Columns = {0,2,1,2,1,4,5,6,5,7,8,7,9,3,3};
        static readonly int[] Layers  = {0,1,1,1,1,2,3,3,3,4,4,4,5,2,2};
        static readonly int[] Routes  = {-1,2,0,2,1,2,0,2,1,0,2,1,2,0,1};
        static readonly int[] Regions = {0,1,1,1,1,2,2,2,2,2,2,2,3,2,2};
        static readonly int[] Retention = {0,0,1,1,8,24,40,56,64,72,80,84,88,92,94,96,98,99,100,100,100};
        readonly int[] slots;
        readonly byte[] familiarity;
        public readonly int CombinationCost;

        public PositionAbility(byte[] positions)
        {
            if (positions == null || positions.Length != Columns.Length || positions.Any(v => v > 20))
                throw new InvalidDataException("实际位置熟练度无效。");
            familiarity = (byte[])positions.Clone();
            // Two mirrored slots can share a column. Only the higher retention can win.
            slots = Enumerable.Range(0, positions.Length).Where(i => positions[i] >= 2)
                .GroupBy(i => Columns[i]).Select(g => g.OrderByDescending(i => positions[i]).First()).ToArray();
            if (slots.Length == 0) throw new InvalidDataException("球员缺少可用于估算 CA 的实际位置。");
            var layers = new bool[6]; var routes = new bool[3]; var regions = new bool[4];
            int sum = 0;
            for (int i = 0; i < positions.Length; ++i)
            {
                sum += Math.Max(0, positions[i] - 10);
                if (positions[i] < 15) continue;
                layers[Layers[i]] = regions[Regions[i]] = true;
                if (Routes[i] >= 0) routes[Routes[i]] = true;
            }
            int regionCount = regions.Count(b => b);
            if (regions[0] && regionCount > 1) regionCount += 2;
            if (regions[1] && regions[3]) regionCount = Math.Max(3, regionCount);
            if (layers[2] && layers[4]) regionCount = Math.Max(2, regionCount);
            sum += 3 * (layers.Count(b => b) + routes.Count(b => b));
            if (layers.Skip(2).Take(3).Count(b => b) >= 2) sum -= 3;
            if (routes[0] || routes[1]) sum -= 3;
            if (regionCount >= 2) sum += regionCount * regionCount * (regionCount + 1);
            CombinationCost = Math.Max(0, Math.Min(100, sum - 40));
        }

        public bool Counts(int index) { return slots.Any(s => Weights[index, Columns[s]] > 0); }

        public int Estimate(byte[] attributes) { int slot; return Estimate(attributes, out slot); }
        public int Estimate(byte[] attributes, out int limitingSlot)
        {
            int best = 0; limitingSlot = -1;
            foreach (int slot in slots)
            {
                int candidate = EstimateColumn(attributes, Columns[slot]);
                int retained = Math.Max(1, (candidate * Retention[familiarity[slot]] + 50) / 100);
                if (retained > best) { best = retained; limitingSlot = slot; }
            }
            return best; // Deliberately uncapped. A displayed 200 must never buy extra attributes.
        }

        int EstimateColumn(byte[] values, int column)
        {
            double sum = 0, total = 0; int count = 0;
            for (int i = 0; i < 54; ++i)
            {
                int weight = Weights[i,column];
                if (weight == 0) continue;
                sum += values[i] * weight; total += weight; ++count;
            }
            int firstFoot = values[25] == 100 ? values[24] : values[25];
            bool bothFeet = values[24] < 100 && values[25] < 100;
            double footWeight = Feet[column];
            sum += firstFoot * footWeight; total += footWeight; ++count;
            if (bothFeet) { sum += values[24] * footWeight; total += footWeight; ++count; }
            int initial = Math.Max(1, (int)Math.Floor((sum / total / 5.0 - 6) * 20));
            double threshold = 3.75 * total / count;
            int candidate = initial, previous = 0;
            for (int pass = 1; pass <= 100; ++pass)
            {
                int deduction = Math.Min(CombinationCost / 3, candidate / 4);
                double baseline = ((candidate - deduction) / 20.0 + 6) * 5;
                double excess = 0;
                for (int i = 0; i < 54; ++i)
                    excess += Excess(values[i], Weights[i,column], baseline, threshold);
                excess += Excess(firstFoot, footWeight, baseline, threshold);
                if (bothFeet) excess += Excess(values[24], footWeight, baseline, threshold);
                double error = baseline - (sum + excess * 3.75) / total;
                int correction = error >= 0 ? Math.Max(1,(int)(error + .5)) : (int)(error - .5);
                int limit = Math.Max(1, 5 - pass / 10);
                correction = Math.Max(-limit, Math.Min(limit, correction));
                if (correction == 0) break;
                if (pass > 1)
                {
                    if (Math.Abs(correction - previous) >= 6 && Math.Abs(correction) >= 2) correction /= 2;
                    if (Math.Abs(correction) > Math.Abs(previous)) correction = Math.Sign(correction) * Math.Abs(previous);
                    if (correction == -previous && Math.Abs(correction) > 1) correction -= Math.Sign(correction);
                    if (previous < 0) correction = Math.Max(previous, correction);
                }
                previous = correction;
                candidate -= Math.Sign(correction);
                if (candidate <= 0) candidate = initial;
            }
            return candidate;
        }

        static double Excess(int value, double weight, double baseline, double threshold)
        {
            double deviation = (value - baseline) * weight / 5;
            if (deviation > threshold) return deviation - threshold;
            if (deviation < -threshold) return deviation + threshold;
            return 0;
        }
    }
}
