// Modified build v1.1.17, 2026-11-16. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
// Role/attribute facts organised from the author's FM26 observations, not SI-authored FM27 weights.
// https://community.sports-interactive.com/forums/topic/600770-football-manager-26-roles-attributes/
// These are editable development presets; they neither detect nor modify the active tactical role.
using System.Collections.Generic;

namespace Fm24Editor
{
    static class GrowthRoleCatalog
    {
        // FM27.0.x match-engine performance, Scout Reporter benchmarks.ts / FM Match Lab Study 13b.
        // Select weights >=8 and keep at least two attributes per group. The stored priority is binary.
        // GK: agility (10), reflexes (9). FMSuperScout's separate keeper Meta table leads with the same pair.
        // CB: jumping reach (10), acceleration (6, runner-up).
        // FB and WB: acceleration (10), pace (10).
        // DM and CM: acceleration (10), pace (8).
        // AM, WM and winger: pace (10), acceleration (9).
        // ST: acceleration (10), pace (10), jumping reach (9).
        static readonly int[] KeeperPriority = {21,46};
        static readonly int[] CentreBackPriority = {34,39};
        static readonly int[] PacePriority = {34,38};
        static readonly int[] StrikerPriority = {34,38,39};

        static int[] PriorityFor(string group)
        {
            switch(group)
            {
                case "门将": return KeeperPriority;
                case "中后卫": return CentreBackPriority;
                case "中锋": return StrikerPriority;
                case "边后卫":
                case "翼卫":
                case "后腰":
                case "中场":
                case "前腰":
                case "边前卫":
                case "边锋": return PacePriority;
                default: return null;
            }
        }

        internal static GrowthProfile Preset(string name,int[] core,int[] support,string group=null,
            string phase="基本",string english="")
        {
            return new GrowthProfile(name,core,support,group,phase,english,PriorityFor(group??name));
        }

        public static IEnumerable<GrowthProfile> Create()
        {
            yield return Preset("边前卫",new[]{0,7,23,18,28,29,38,37},new[]{1,22,17,52,6,10,34,46},"边前卫","基本","");
            yield return Preset("拖后中锋",new[]{2,22,23,52,6,36},new[]{1,7,17,18,28,10,42},"中锋","持球","Deep-Lying Forward");
            yield return Preset("肋部中锋",new[]{1,2,22,23,52,6,29,34},new[]{0,3,7,17,18,46,42,38,37},"中锋","持球","Channel Forward");
            yield return Preset("中锋",new[]{2,22,3,23,52,6,34,36},new[]{1,7,17,18,46,42,39,38},"中锋","持球","Centre Forward");
            yield return Preset("支点中锋",new[]{2,3,45,43,52,6,42,39,36},new[]{22,17,18,28},"中锋","持球","Target Forward");
            yield return Preset("抢点型前锋",new[]{2,3,17,52,53,6,34},new[]{22,23,18,42},"中锋","持球","Poacher");
            yield return Preset("伪九号",new[]{1,22,7,23,52,18,6,28,10,34},new[]{2,17,26,46,42},"中锋","持球","False Nine");
            yield return Preset("内切型边锋",new[]{1,22,23,52,28,34,46},new[]{0,4,7,17,26,6,10,29,42,38,37},"边锋","持球","Inside Winger");
            yield return Preset("边路前锋",new[]{1,22,23,17,6,34,46,38},new[]{0,2,7,52,26,29,42,37},"边锋","持球","Wide Forward");
            yield return Preset("内切型前锋",new[]{1,22,23,17,52,6,34,46},new[]{0,2,4,7,26,10,29,42,38,37},"边锋","持球","Inside Forward");
            yield return Preset("边锋",new[]{0,1,23,28,34,46,38},new[]{22,7,17,26,6,29,42,37},"边锋","持球","Winger");
            yield return Preset("组织型边锋",new[]{0,1,22,7,23,52,18,6,28,10,34},new[]{17,26,29,46,38,37},"边锋","持球","Playmaking Winger");
            yield return Preset("二前锋",new[]{2,22,17,52,6,34},new[]{1,4,7,23,53,18,29,46,38,37},"前腰","持球","Second Striker");
            yield return Preset("自由进攻核心",new[]{1,22,4,7,23,52,26,6,10},new[]{0,2,17,18,34,46},"前腰","持球","Free Role");
            yield return Preset("前腰",new[]{22,4,7,23,52,26,6},new[]{0,1,2,17,18,10,34,46},"前腰","持球","Attacking Midfielder");
            yield return Preset("前场组织核心",new[]{22,7,23,52,18,6,28,10},new[]{0,1,17,26,34,46},"前腰","持球","Advanced Playmaker");
            yield return Preset("肋部中场",new[]{0,22,7,23,52,6,29,34},new[]{1,4,17,18,26,10,46,38,37},"前腰","持球","Channel Midfielder");
            yield return Preset("内切型边锋",new[]{1,22,23,52,28,34,46},new[]{0,4,7,17,26,6,10,29,42,38,37},"边前卫","持球","Inside Winger");
            yield return Preset("边锋",new[]{0,1,23,28,34,46,38},new[]{22,7,17,26,6,29,42,37},"边前卫","持球","Winger");
            yield return Preset("组织型边锋",new[]{0,1,22,7,23,52,18,6,28,10,34},new[]{17,26,29,46,38,37},"边前卫","持球","Playmaking Winger");
            yield return Preset("边前卫",new[]{0,7,23,18,28,29,38,37},new[]{1,22,17,52,6,10,34,46},"边前卫","持球","Wide Midfielder");
            yield return Preset("前腰",new[]{22,4,7,23,52,26,6},new[]{0,1,2,17,18,10,34,46},"中场","持球","Attacking Midfielder");
            yield return Preset("前场组织核心",new[]{22,7,23,52,18,6,28,10},new[]{0,1,17,26,34,46},"中场","持球","Advanced Playmaker");
            yield return Preset("肋部中场",new[]{0,22,7,23,52,6,29,34},new[]{1,4,17,18,26,10,46,38,37},"中场","持球","Channel Midfielder");
            yield return Preset("中场组织核心",new[]{22,7,23,52,18,6,28,10},new[]{1,9,17,26,20,29,46,37},"中场","持球","Midfield Playmaker");
            yield return Preset("侧翼中前卫",new[]{22,7,9,18,28},new[]{0,1,23,17,52,53,6,20,10,29,46,37},"中场","持球","Wide Central Midfielder");
            yield return Preset("中前卫",new[]{22,7,9,18,28},new[]{23,17,52,53,6,20,10,29,37},"中场","持球","Central Midfielder");
            yield return Preset("拖后组织核心",new[]{22,7,23,52,18,28,10},new[]{5,17,53,6,20,29,42,37,9},"后腰","持球","Deep-Lying Playmaker");
            yield return Preset("全能组织核心",new[]{22,7,23,52,18,6,28,10,29,37},new[]{1,4,5,17,38,9,20,34,46,42},"后腰","持球","Box-to-Box Playmaker");
            yield return Preset("全能中场",new[]{7,9,6,28,29,37},new[]{1,2,22,4,23,45,17,34,42,38,36,52,18},"后腰","持球","Box-to-Box Midfielder");
            yield return Preset("防守型后腰",new[]{9,17,53,20,28},new[]{22,5,7,45,29,37,36,52,18},"后腰","持球","Defensive Midfielder");
            yield return Preset("半后卫",new[]{3,5,9,17,53,20,28,39,36},new[]{22,7,45,29,37,43,52,18},"后腰","持球","Half Back");
            yield return Preset("进攻型翼卫",new[]{0,1,23,6,28,29,34,46,38,37},new[]{22,5,7,9,17,18,26,20,42},"翼卫","持球","Advanced Wing-Back");
            yield return Preset("翼卫",new[]{0,5,9,28,29,34,38,37},new[]{1,22,7,23,17,53,18,6,20,46,42},"翼卫","持球","Wing-Back");
            yield return Preset("组织型翼卫",new[]{22,7,9,23,52,18,28,10,34},new[]{0,1,5,17,53,6,20,29,46,38,37},"翼卫","持球","Playmaking Wing-Back");
            yield return Preset("内收型翼卫",new[]{7,9,17,52,18,20,28,34},new[]{22,5,23,53,29,46,38,37},"翼卫","持球","Inside Wing-Back");
            yield return Preset("翼卫",new[]{0,5,9,28,29,34,38,37},new[]{1,22,7,23,17,53,18,6,20,46,42},"边后卫","持球","Wing-Back");
            yield return Preset("边后卫",new[]{5,9,17,53,20,28,34},new[]{0,1,7,23,18,29,46,38,37},"边后卫","持球","Full-Back");
            yield return Preset("组织型翼卫",new[]{22,7,9,23,52,18,28,10,34},new[]{0,1,5,17,53,6,20,29,46,38,37},"边后卫","持球","Playmaking Wing-Back");
            yield return Preset("内收型翼卫",new[]{7,9,17,52,18,20,28,34},new[]{22,5,23,53,29,46,38,37},"边后卫","持球","Inside Wing-Back");
            yield return Preset("内收型边后卫",new[]{3,5,9,17,20,36},new[]{1,45,43,52,53,18,29,34,46,39,38,37},"边后卫","持球","Inside Full-Back");
            yield return Preset("出球中后卫",new[]{3,5,7,9,17,52,20,39,36},new[]{22,23,45,43,53,18,10,38},"中后卫","持球","Ball-Playing Centre-Back");
            yield return Preset("压上型中后卫",new[]{3,5,7,9,23,17,52,18,20,28,39,36},new[]{22,1,45,43,53,10,38,37},"中后卫","持球","Advanced Centre-Back");
            yield return Preset("套边型中后卫",new[]{0,3,5,9,17,29,39,38,37,36},new[]{1,23,45,43,52,53,18,6,20,34,46},"中后卫","持球","Overlapping Centre-Back");
            yield return Preset("边中卫",new[]{3,5,9,17,20,39,36},new[]{1,45,43,52,53,18,29,34,46,38,37},"中后卫","持球","Wide Centre-Back");
            yield return Preset("防守型中后卫",new[]{3,5,9,17,20,39,36},new[]{45,43,53,38},"中后卫","持球","No-Nonsense Centre-Back");
            yield return Preset("中后卫",new[]{3,5,9,17,20,39,36},new[]{45,43,52,53,18,38},"中后卫","持球","Centre-Back");
            yield return Preset("稳健型门将",new[]{12,13,14,11,21,53,20,46},new[]{19,17,18},"门将","持球","No-Nonsense Goalkeeper");
            yield return Preset("出球型门将",new[]{12,13,14,11,15,21,53,20,46},new[]{19,7,16,17,52,18},"门将","持球","Ball-Playing Goalkeeper");
            yield return Preset("门将",new[]{12,13,14,11,21,53,20,46},new[]{15,19,16,17,18},"门将","持球","Goalkeeper");
            yield return Preset("中锋",new[]{17,18,29},new[]{5,45,6,28,37},"中锋","无球","Centre Forward");
            yield return Preset("回防型中锋",new[]{45,17,18,28,29,37},new[]{5,6,20},"中锋","无球","Tracking Centre Forward");
            yield return Preset("拉边反击中锋",new[]{17,53,18,6,28,38},new[]{22,5,52,34},"中锋","无球","Splitting Outlet Centre Forward");
            yield return Preset("中路反击中锋",new[]{17,53,18,6,28,42},new[]{22,5,52,36},"中锋","无球","Central Outlet Centre Forward");
            yield return Preset("边锋",new[]{34,17,18,28,29},new[]{45,5,6,46,38,37},"边锋","无球","Winger");
            yield return Preset("回防型边锋",new[]{45,17,18,28,29,34,37},new[]{5,9,6,20,46,38},"边锋","无球","Tracking Winger");
            yield return Preset("边路反击边锋",new[]{17,53,18,6,28,38},new[]{1,22,5,52,34,46},"边锋","无球","Wide Outlet Winger");
            yield return Preset("内收反击边锋",new[]{17,53,18,6,28,42},new[]{1,22,5,52,34,36},"边锋","无球","Inside Outlet Winger");
            yield return Preset("前腰",new[]{17,18,29},new[]{5,45,6,28,37},"前腰","无球","Attacking Midfielder");
            yield return Preset("中路反击前腰",new[]{17,53,18,6,28,42},new[]{22,5,52,36},"前腰","无球","Central Outlet Attacking Midfielder");
            yield return Preset("拉边反击前腰",new[]{17,53,18,6,28,38},new[]{22,5,52,34},"前腰","无球","Splitting Outlet Attacking Midfielder");
            yield return Preset("回防型前腰",new[]{45,17,18,28,29,37},new[]{5,6,20},"前腰","无球","Tracking Attacking Midfielder");
            yield return Preset("边路反击边前卫",new[]{17,53,18,6,28,38},new[]{1,22,5,52,34,46},"边前卫","无球","Wide Outlet Wide Midfielder");
            yield return Preset("边前卫",new[]{18,28,29,34},new[]{5,45,17,6,46,38,37},"边前卫","无球","Wide Midfielder");
            yield return Preset("回防型边前卫",new[]{45,17,18,28,29,34,37},new[]{5,9,6,20,46,38},"边前卫","无球","Tracking Wide Midfielder");
            yield return Preset("压迫型中前卫",new[]{9,45,17,18,28,29,37},new[]{5,53,20,38,36},"中场","无球","Pressing Central Midfielder");
            yield return Preset("侧翼补位中前卫",new[]{5,9,18,28,29,46,37},new[]{45,17,53,20,38},"中场","无球","Wide Covering Central Midfielder");
            yield return Preset("中前卫",new[]{9,18,28,29},new[]{5,45,17,53,20,37},"中场","无球","Central Midfielder");
            yield return Preset("屏障型中前卫",new[]{5,9,53,18,20},new[]{17,28,29,37},"中场","无球","Screening Central Midfielder");
            yield return Preset("压迫型后腰",new[]{9,45,17,18,20,28,29,37},new[]{5,53,38,36},"后腰","无球","Pressing Defensive Midfielder");
            yield return Preset("防守型后腰",new[]{9,17,18,20,28,29},new[]{5,45,53,37,36},"后腰","无球","Defensive Midfielder");
            yield return Preset("侧翼补位后腰",new[]{5,9,17,18,20,28,29,46,37},new[]{45,53,38,36},"后腰","无球","Wide Covering Defensive Midfielder");
            yield return Preset("屏障型后腰",new[]{5,9,17,53,18,20},new[]{28,29,37,36},"后腰","无球","Screening Defensive Midfielder");
            yield return Preset("后撤型后腰",new[]{5,9,17,53,18,20,28,29,39,36},new[]{45,37},"后腰","无球","Dropping Defensive Midfielder");
            yield return Preset("压迫型翼卫",new[]{5,9,45,17,20,28,29,34,37},new[]{53,43,18,46,38},"翼卫","无球","Pressing Wing-Back");
            yield return Preset("翼卫",new[]{5,9,17,20,28,34,37},new[]{45,53,18,29,46,38},"翼卫","无球","Wing-Back");
            yield return Preset("留守型翼卫",new[]{5,9,17,53,18,20,34},new[]{28,46,38,37},"翼卫","无球","Holding Wing-Back");
            yield return Preset("压迫型边后卫",new[]{5,9,45,17,20,28,29,34},new[]{43,53,18,46,38,37},"边后卫","无球","Pressing Full-Back");
            yield return Preset("留守型边后卫",new[]{5,9,17,53,18,20,34},new[]{28,46,38},"边后卫","无球","Holding Full-Back");
            yield return Preset("边后卫",new[]{5,9,17,20,28,34},new[]{45,53,18,29,46,38,37},"边后卫","无球","Full-Back");
            yield return Preset("补位型边中卫",new[]{3,5,9,17,53,18,20,39,38,36},new[]{43,34,46},"中后卫","无球","Covering Wide Centre-Back");
            yield return Preset("边中卫",new[]{3,5,9,17,20,39,36},new[]{45,43,53,18,34,46,38},"中后卫","无球","Wide Centre-Back");
            yield return Preset("补位型中后卫",new[]{3,5,9,17,53,18,20,39,38,36},new[]{43,34},"中后卫","无球","Covering Centre-Back");
            yield return Preset("中后卫",new[]{3,5,9,17,20,39,36},new[]{45,43,52,53,18,38},"中后卫","无球","Centre-Back");
            yield return Preset("上抢型边中卫",new[]{3,5,9,45,17,43,20,39,36},new[]{53,18,34,46,38},"中后卫","无球","Stopping Wide Centre-Back");
            yield return Preset("上抢型中后卫",new[]{3,5,9,45,17,43,20,39,36},new[]{53,18,38},"中后卫","无球","Stopping Centre-Back");
            yield return Preset("门将",new[]{12,13,14,11,21,53,20,46},new[]{19,17,18,32},"门将","无球","Goalkeeper");
            yield return Preset("清道夫门将",new[]{12,13,14,11,19,21,32,17,18,46},new[]{53,20,34},"门将","无球","Sweeper Keeper");
            yield return Preset("门线型门将",new[]{12,13,14,11,21,53,20,46},new[]{19,17,18},"门将","无球","Line-Holding Goalkeeper");
        }
    }
}
