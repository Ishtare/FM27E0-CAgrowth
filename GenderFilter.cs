// Modified build notice: FM27E0-CAgrowth v1.1.20; changes dated 2026-11-18.
// See CHANGELOG.md; GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Linq;
namespace Fm24Editor
{
    static class GenderFilter
    {
        // 27.0.1+2494687, independently read from Ronaldo, Messi, Haaland, Saka, Son,
        // Sam Kerr, Bronze, Russo, Putellas, Bonmati, Wang Shuang, Renard, Hegerberg and Banda.
        // Person+0x19 bit 0x10 is female. A separate club+0xB9 flag is used as a cross-check.
        // Person flags also work for free agents, unlike filtering by the employer alone.
        public static bool? Female(byte[] head,string version,bool verifiedCurrentBuild=false)
        {
            if((version!="27.0.1+2494687" && !(NativeEngineLayout.KnownVersion(version) && verifiedCurrentBuild)) || head==null || head.Length<=0x19)return null;
            return (head[0x19]&0x10)!=0;
        }
        public static void Apply(FmProcess fm,ScanResult result)
        {
            result.RawPlayers=result.Players.Count;
            if(fm.Version!="27.0.1+2494687" && !(CurrentBuildProfile.Matches(fm))) {result.GenderNote="女足筛选：当前游戏版本尚未验证";return;}
            int compared=0, mismatch=0;var clubs=new Dictionary<ulong,int>();
            foreach(var p in result.Players.Where(p=>p.Club!=0 && p.Female.HasValue))
            {
                int gender;
                if(!clubs.TryGetValue(p.Club,out gender))
                {
                    var flag=fm.Read(p.Club+0xB9,1);
                    gender=fm.ReadPtr(p.Club)==fm.Base+FmProcess.ClubVtRva && flag!=null && flag[0]<=1?flag[0]:-1;
                    clubs[p.Club]=gender;
                }
                if(gender<0)continue;compared++;if(p.Female.Value!=(gender==1))mismatch++;
            }
            if(compared<20 || mismatch>0 || result.Players.Any(p=>!p.Female.HasValue))
            {result.GenderNote="女足筛选未通过交叉检查（"+mismatch+" / "+compared+"），名单暂按原始数据显示";return;}
            result.GenderVerified=true;
            result.HiddenWomen=result.Players.RemoveAll(p=>p.Female==true);
            result.HiddenWomenClubs=result.Clubs.RemoveAll(c=>
            {
                var flag=fm.Read(c.Addr+0xB9,1);return flag!=null && flag[0]==1;
            });
            result.GenderNote="已隐藏女足球员 "+result.HiddenWomen.ToString("N0")+" 人、女足俱乐部 "+result.HiddenWomenClubs+" 家";
        }
    }
}
