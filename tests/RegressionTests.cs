// Modified build v1.1.24, 2026-11-20. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace Fm24Editor
{
    static class RegressionTests
    {
        static int assertions;
        static void Check(bool ok,string message) { assertions++;if(!ok)throw new Exception(message); }
        static void Throws(Action action,string message) {bool threw=false;try{action();}catch{threw=true;}Check(threw,message);}
        static void ApplyDirectoryFixture(List<Player> roster)
        {
            // Two CCTV directory IDs identify the same Li Ke. A real FM roster has one such person.
            Player liKe=null;
            for(int i=0;i<roster.Count;i++)
                if(roster[i].Born==new DateTime(1993,5,24) && new[]{"like","keli"}.Contains(PlayerNames.Normalize(roster[i].Name)))
                {if(liKe==null)liKe=roster[i];else roster[i]=liKe;}
            PlayerNames.ApplyOfficial(roster.Distinct().ToList());
        }
        class Memory
        {
            public Dictionary<ulong,byte> Bytes=new Dictionary<ulong,byte>();public int Writes,FailWrite;
            public byte[] Read(ulong a,int n) {var b=new byte[n];for(int i=0;i<n;i++)if(!Bytes.TryGetValue(a+(ulong)i,out b[i]))return null;return b;}
            public void Put(ulong a,byte[] b) {for(int i=0;i<b.Length;i++)Bytes[a+(ulong)i]=b[i];}
            public bool Write(ulong a,byte[] b) {Writes++;if(Writes==FailWrite)return false;Put(a,b);return true;}
        }
        static uint Date(int y,int m,int d) {return ((uint)y<<16)|0x1A00u|(uint)new DateTime(y,m,d).DayOfYear;}
        static readonly ulong manager=0x10000,vt=0x90000,person=0x20000,dates=0x30000,people=0x40000,contract=0x70000;
        static Player Player() {return new Player{Person=person,Uid=123};}
        static Memory Fixture()
        {
            var m=new Memory();m.Put(manager,new byte[0x108]);m.Put(manager,BitConverter.GetBytes(vt));m.Put(manager+8,BitConverter.GetBytes(vt+16));
            m.Put(person+8,BitConverter.GetBytes(5).Concat(BitConverter.GetBytes(123)).ToArray());
            m.Put(person+0x18,new byte[]{0xAD,0x92,0x40,0x18,0x04});
            foreach(var tuple in new[]{new[]{0x30,8,0x30000},new[]{0x90,16,0x40000}})
                m.Put(manager+(ulong)tuple[0],BitConverter.GetBytes((ulong)tuple[2]).Concat(BitConverter.GetBytes((ulong)(tuple[2]+3*tuple[1]))).Concat(BitConverter.GetBytes((ulong)(tuple[2]+3*tuple[1]))).ToArray());
            int[] indices={7,5,1};
            for(int i=0;i<3;i++)
            {
                ulong a=0x50000+(ulong)i*24;var rec=new byte[24];BitConverter.GetBytes(indices[i]).CopyTo(rec,0);BitConverter.GetBytes(-1).CopyTo(rec,4);
                BitConverter.GetBytes(Date(2024,11,30)).CopyTo(rec,8);
                // use valid dates and strictly increasing order
                BitConverter.GetBytes(i==0?Date(2027,5,31):i==1?Date(2027,6,30):Date(2028,6,30)).CopyTo(rec,12);
                BitConverter.GetBytes(Date(2024,11,29)).CopyTo(rec,16);rec[22]=5;m.Put(a,rec);m.Put(dates+(ulong)i*8,BitConverter.GetBytes(a));
            }
            for(int i=0;i<3;i++)m.Put(people+(ulong)i*16,BitConverter.GetBytes(new[]{1,5,7}[i]).Concat(new byte[4]).Concat(BitConverter.GetBytes(0x50000UL+(ulong)(2-i)*24)).ToArray());
            m.Put(contract,BitConverter.GetBytes(Date(2027,6,30)));return m;
        }
        [STAThread]
        static int Main()
        {
            try
            {
                Check(PlayerNames.CanSearch("梅西"),"two-character Chinese names");Check(PlayerNames.CanSearch("C罗"),"mixed Chinese aliases");Check(!PlayerNames.CanSearch("梅"),"short queries");
                Check(PlayerNames.Matches("Kylian Mbappé","  MBAPPE "),"accent/case normalization");Check(PlayerNames.Matches("克里斯蒂亚诺·罗纳尔多","克里斯蒂亚诺罗纳尔多"),"Chinese separators");
                Check(!PlayerNames.Matches("abc|def","cde"),"never match across independent name entries");
                Check(PlayerNames.Normalize("Martin Ødegaard")==PlayerNames.Normalize("Martin Odegaard"),"non-decomposing Scandinavian letters");
                Check(PlayerNames.Normalize("Łukasz Groß")==PlayerNames.Normalize("Lukasz Gross"),"Polish and German name variants");
                Check(PlayerNames.CompatibleFullNames("Gabriel Magalhaes","Gabriel dos Santos Magalhães"),"verified English names may include middle names");
                Check(!PlayerNames.CompatibleFullNames("Magalhaes","Gabriel dos Santos Magalhães"),"a surname alone cannot bind a person");
                Check(!PlayerNames.CompatibleFullNames("G Magalhaes","Gabriel dos Santos Magalhães"),"an initial cannot bind a person");
                Check(!PlayerNames.CompatibleFullNames("Gabriel Silva","Gabriel dos Santos Magalhães"),"different surname cannot bind a person");
                PlayerNames.Load(AppDomain.CurrentDomain.BaseDirectory);Check(PlayerNames.Count==5270,"full embedded expanded catalog");
                Check(PlayerNames.CoverageNote.Contains("96") && PlayerNames.CoverageNote.Contains("3,385") && PlayerNames.CoverageNote.Contains("中超 16") && PlayerNames.CoverageNote.Contains("5,270"),"coverage facts embedded for offline review");
                Check(PlayerNames.CanSearchPlayer("塔"),"a published one-character Chinese name is searchable");
                Check(!PlayerNames.CanSearchPlayer("梅") && !PlayerNames.CanSearchPlayer("a"),"unrecognized short queries do not flood the results");
                Check(PlayerNames.Matches("乔纳森·塔|塔","塔") && !PlayerNames.Matches("塔瓦雷斯","塔"),"one-character Chinese searches require an exact alias");
                Check(!PlayerNames.CanSearch("塔"),"staff and club searches retain their original length limit");
                var catalog=new List<OfficialName>();
                using(var stream=typeof(PlayerNames).Assembly.GetManifestResourceStream("FM27E0.official-names.tsv"))
                using(var reader=new StreamReader(stream,Encoding.UTF8))catalog=PlayerNames.Read(reader);
                var fiveLeagues=catalog.Skip(106).Take(3385).ToList();
                Check(fiveLeagues.Count==3385 && fiveLeagues.All(e=>e.Uid==0),"original five-league identities retained");
                var expanded=catalog.Skip(106).Take(5162).ToList();
                Check(expanded.Count==5162 && expanded.All(e=>e.Uid==0) && expanded.Skip(3385).All(e=>e.Born.HasValue),"CCTV IDs must never become FM UIDs; supplementary identities have birthdays");
                var roster=expanded.Select((e,i)=>new Player {Name=e.English.Split('|')[0],Search=e.English.Split('|')[0],Uid=1500000000+i,Born=e.Born}).ToList();
                // CCTV has two directory IDs for Li Ke (1993-05-24); they are two sources for one FM person.
                var liKeIndices=expanded.Select((e,i)=>new {Entry=e,Index=i}).Where(x=>x.Entry.Chinese=="李可" && x.Entry.Born==new DateTime(1993,5,24)).Select(x=>x.Index).ToList();
                Check(liKeIndices.Count==2,"known duplicate directory sources retained for Li Ke");
                ApplyDirectoryFixture(roster);
                for(int i=0;i<expanded.Count;i++)
                    Check(roster[i].ChineseName!=null && roster[i].Matches(expanded[i].Chinese) && expanded[i].Aliases.Split('|').All(a=>roster[i].Matches(a)) && expanded[i].English.Split('|').All(a=>roster[i].Matches(a)),"sourced roster identity, Chinese and English aliases: "+expanded[i].English+" -> "+(roster[i].NameAliases??"(unmatched)"));
                var supplementary=expanded.Skip(3385).ToList();
                for(int formIndex=0;formIndex<supplementary.Max(e=>e.English.Split('|').Length);formIndex++)
                {
                    var current=supplementary.Where(e=>e.English.Split('|').Length>formIndex).ToList();
                    var variants=current.Select((e,i)=>new Player{Name=e.English.Split('|')[formIndex],Search=e.English.Split('|')[formIndex],Uid=1502000000+i,Born=e.Born}).ToList();
                    ApplyDirectoryFixture(variants);
                    for(int i=0;i<current.Count;i++)
                        Check(variants[i].Matches(current[i].Chinese),"supplementary English order binds Chinese with the same birthday: "+variants[i].Name);
                }
                var benzema=new Player{Name="Karim Benzema",Search="Karim Benzema",Uid=8435089,Born=new DateTime(1987,12,19)};
                PlayerNames.ApplyOfficial(new[]{benzema});
                Check(benzema.ChineseName=="本泽马" && benzema.Matches("本泽马") && benzema.Matches("卡里姆·本泽马") && benzema.Matches("Benzema") && benzema.Matches("karim benzema"),"Benzema Chinese/English/full name regression");
                benzema.Born=new DateTime(2008,12,19);PlayerNames.ApplyOfficial(new[]{benzema});
                Check(benzema.ChineseName=="本泽马","verified Benzema UID and original name survive age changes");
                benzema.Name=benzema.Search="Benzema";PlayerNames.ApplyOfficial(new[]{benzema});
                Check(benzema.ChineseName=="本泽马","verified UID also accepts the original English short name");
                benzema.Name=benzema.Search="Unrelated Player";PlayerNames.ApplyOfficial(new[]{benzema});
                Check(benzema.ChineseName==null,"Benzema UID cannot translate an unrelated original name");
                benzema.Uid=1502000001;benzema.Name=benzema.Search="Karim Benzema";benzema.Born=new DateTime(1987,12,19);PlayerNames.ApplyOfficial(new[]{benzema});
                Check(benzema.ChineseName=="本泽马","full Benzema identity also works without a known UID");
                benzema.Born=new DateTime(1987,12,18);PlayerNames.ApplyOfficial(new[]{benzema});
                Check(benzema.ChineseName==null,"Benzema fallback rejects a different birthday");
                benzema.Name=benzema.Search="Benzema";benzema.Born=new DateTime(1987,12,19);PlayerNames.ApplyOfficial(new[]{benzema});
                Check(benzema.ChineseName==null,"surname plus birthday without verified UID cannot bind Benzema");
                var chinesePlayer=new Player{Name="Ma Zhen",Search="Ma Zhen",Uid=1502000002,Born=new DateTime(1998,6,1)};
                PlayerNames.ApplyOfficial(new[]{chinesePlayer});Check(chinesePlayer.Matches("马镇"),"Chinese family-name-first pinyin");
                chinesePlayer.Name=chinesePlayer.Search="Zhen Ma";PlayerNames.ApplyOfficial(new[]{chinesePlayer});Check(chinesePlayer.Matches("马镇"),"Chinese given-name-first pinyin");
                chinesePlayer.Born=new DateTime(1998,6,2);PlayerNames.ApplyOfficial(new[]{chinesePlayer});Check(chinesePlayer.ChineseName==null,"pinyin variants cannot bypass birthday checks");
                chinesePlayer.Born=null;PlayerNames.ApplyOfficial(new[]{chinesePlayer});Check(chinesePlayer.ChineseName==null,"supplementary identity cannot bind when the game birthday is unavailable");
                chinesePlayer.Born=new DateTime(1998,6,1);
                var sameChineseName=new Player{Name="Ma Zhen",Search="Ma Zhen",Uid=1502000005,Born=chinesePlayer.Born};
                PlayerNames.ApplyOfficial(new[]{chinesePlayer,sameChineseName});Check(chinesePlayer.ChineseName==null && sameChineseName.ChineseName==null,"two genuine candidates with the same pinyin and birthday remain unbound");
                var wuLei=new Player{Name="Wu Lei",Search="Wu Lei",Uid=1502000003,Born=new DateTime(1991,11,19)};
                var mane=new Player{Name="Sadio Mane",Search="Sadio Mane",Uid=1502000004,Born=new DateTime(1992,4,10)};
                PlayerNames.ApplyOfficial(new[]{wuLei,mane});Check(wuLei.Matches("武磊") && mane.Matches("马内") && mane.Matches("萨迪奥·马内"),"Chinese league and non-five-league star coverage");
                var odegaard=new Player {Name="Martin Ødegaard",Search="Martin Ødegaard",Uid=1501000000,Born=new DateTime(1998,12,17)};
                var yamal=new Player {Name="Lamine Yamal",Search="Lamine Yamal",Uid=1501000001,Born=new DateTime(2007,7,13)};
                var musiala=new Player {Name="Jamal Musiala",Search="Jamal Musiala",Uid=1501000002,Born=new DateTime(2003,2,26)};
                var doue=new Player {Name="Désiré Doué",Search="Désiré Doué",Uid=1501000003,Born=new DateTime(2005,6,3)};
                var bastoni=new Player {Name="Alessandro Bastoni",Search="Alessandro Bastoni",Uid=1501000004,Born=new DateTime(1999,4,13)};
                PlayerNames.ApplyOfficial(new[]{odegaard,yamal,musiala,doue,bastoni});
                Check(odegaard.Matches("厄德高") && odegaard.Matches("马丁·厄德高") && odegaard.Matches("Odegaard"),"Premier League full/short Chinese and unaccented English");
                Check(yamal.Matches("亚马尔") && yamal.Matches("拉明亚马尔"),"La Liga full/short Chinese");
                Check(musiala.Matches("穆西亚拉"),"Bundesliga Chinese name");
                Check(doue.Matches("杜埃") && doue.Matches("Desire Doue"),"Ligue 1 accented English and Chinese");
                Check(bastoni.Matches("巴斯托尼"),"Serie A Chinese name");
                var middle=new Player {Name="Gabriel dos Santos Magalhães",Search="Gabriel dos Santos Magalhães",Uid=1501000005,Born=new DateTime(1997,12,19)};
                PlayerNames.ApplyOfficial(new[]{middle});Check(middle.Matches("加布里埃尔马加良斯"),"middle-name variants still require matching birthday and full identity");
                middle.Born=new DateTime(1997,12,18);PlayerNames.ApplyOfficial(new[]{middle});Check(middle.ChineseName==null,"middle-name fallback rejects a different birthday");
                middle.Born=new DateTime(1997,12,19);
                var middleTwin=new Player {Name="Gabriel Joao Magalhaes",Search="Gabriel Joao Magalhaes",Uid=1501000006,Born=middle.Born};
                PlayerNames.ApplyOfficial(new[]{middle,middleTwin});Check(middle.ChineseName==null && middleTwin.ChineseName==null,"ambiguous middle-name variants cannot bind either person");
                var p=new Player{Name="Cristiano Ronaldo",Search="Cristiano Ronaldo | Cristiano Ronaldo dos Santos Aveiro",Uid=735216,Born=new DateTime(1985,2,5)};
                PlayerNames.ApplyOfficial(new[]{p});Check(p.ChineseName=="克里斯蒂亚诺·罗纳尔多" && p.Matches("C罗") && p.Matches("Ronaldo"),"bilingual same person");
                Check(p.Name=="Cristiano Ronaldo" && p.Search.StartsWith("Cristiano Ronaldo"),"do not rename game data");
                var wrong=new Player{Name=p.Name,Search=p.Search,Uid=p.Uid,Born=new DateTime(2005,2,5)};PlayerNames.ApplyOfficial(new[]{wrong});Check(wrong.ChineseName=="克里斯蒂亚诺·罗纳尔多","stable UID/name identity survives an existing age edit");
                wrong.Name="Different Player";wrong.Search=wrong.Name;PlayerNames.ApplyOfficial(new[]{wrong});Check(wrong.ChineseName==null,"UID alone cannot assign a translated name");
                var changedMessi=new Player{Name="Lionel Messi",Search="Lionel Messi",Uid=7458500,Born=new DateTime(2008,6,24)};PlayerNames.ApplyOfficial(new[]{changedMessi});Check(changedMessi.ChineseName=="梅西" && changedMessi.Matches("梅西"),"known Messi UID survives age edits from an older editor");
                PlayerNames.ApplyOfficial(new[]{p,new Player{Name=p.Name,Search=p.Search,Uid=p.Uid,Born=p.Born}});Check(p.ChineseName==null,"ambiguous identities");
                Throws(()=>PlayerNames.Read(new StringReader("0\tJohn\tbad-date\t约翰\t\thttps://example.org")),"invalid imported date");
                Throws(()=>PlayerNames.Read(new StringReader("0\tJohn\t\t约翰\t\tfile:///private")),"invalid source URL");
                Check(Zh.Z("Runs With Ball Down Left").Contains("左路"),"trait localization");Check(Zh.Z("Authority")=="威信","FM27 staff terminology");
                var memory=Fixture();var baseline=memory.Bytes.ToDictionary(x=>x.Key,x=>x.Value);var snapshot=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());
                Check(snapshot.Record.Date==new DateTime(2027,6,30),"read announced retirement by person index, not UID");
                var plan=snapshot.Plan(new DateTime(2026,7,27),new DateTime(2029,6,30));string error;
                Check(RetirementBatch.Apply(memory.Read,memory.Write,plan,()=>true,out error),"postpone transaction");
                var after=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());Check(after.Record.Date==new DateTime(2029,6,30),"new date and sorted index");
                Check((BitConverter.ToUInt32(after.Record.Raw,12)&0xFE00)==0x1A00,"preserve FM date flags");
                Check(after.HasAnnouncement && memory.Read(person+0x18,5).SequenceEqual(new byte[]{0xAD,0x92,0x40,0x18,0x04}),"postponement preserves the announcement and unrelated person flags");
                Check(memory.Read(contract,4).SequenceEqual(BitConverter.GetBytes(Date(2027,6,30))),"retirement does not change contract expiry");
                var reverse=plan.AsEnumerable().Reverse().Select(w=>new RetirementWrite{Address=w.Address,Old=w.New,New=w.Old,What="undo"}).ToList();
                Check(RetirementBatch.Apply(memory.Read,memory.Write,reverse,()=>true,out error),"postpone undo");
                Check(baseline.All(kv=>memory.Bytes[kv.Key]==kv.Value),"postpone restores original bytes");
                snapshot=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());plan=snapshot.Plan(new DateTime(2026,7,27),null);
                Check(RetirementBatch.Apply(memory.Read,memory.Write,plan,()=>true,out error),"cancel transaction");
                after=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());Check(after.Record==null && after.Records.Count==2,"remove from both indexes");
                Check(!after.HasAnnouncement && memory.Read(person+0x18,5).SequenceEqual(new byte[]{0xAC,0x92,0x40,0x18,0x04}),"cancel removes the transfer-blocking announcement while preserving all other flags");
                Check(memory.Read(contract,4).SequenceEqual(BitConverter.GetBytes(Date(2027,6,30))),"cancel keeps contract expiry unchanged");
                reverse=plan.AsEnumerable().Reverse().Select(w=>new RetirementWrite{Address=w.Address,Old=w.New,New=w.Old,What="undo"}).ToList();
                Check(RetirementBatch.Apply(memory.Read,memory.Write,reverse,()=>true,out error),"cancel undo");Check(baseline.All(kv=>memory.Bytes[kv.Key]==kv.Value),"cancel restores original bytes");
                memory=Fixture();snapshot=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());plan=snapshot.Plan(new DateTime(2026,7,27),new DateTime(2029,6,30));memory.FailWrite=2;
                Check(!RetirementBatch.Apply(memory.Read,memory.Write,plan,()=>true,out error),"failed multi-write operation");Check(baseline.All(kv=>memory.Bytes[kv.Key]==kv.Value),"rollback on write failure");
                memory=Fixture();snapshot=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());plan=snapshot.Plan(new DateTime(2026,7,27),null);memory.FailWrite=plan.Count;
                Check(!RetirementBatch.Apply(memory.Read,memory.Write,plan,()=>true,out error),"failure clearing announcement fails the complete cancellation");
                Check(baseline.All(kv=>memory.Bytes[kv.Key]==kv.Value),"restore both indexes and flag if final cancellation write fails");
                memory=Fixture();snapshot=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());
                var legacyPlan=snapshot.Plan(new DateTime(2026,7,27),null).Where(w=>w.Address!=person+0x18).ToList();
                Check(RetirementBatch.Apply(memory.Read,memory.Write,legacyPlan,()=>true,out error),"simulate the old editor cancelling the record only");
                snapshot=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());
                Check(snapshot.Record==null && snapshot.HasAnnouncement,"detect an already-cancelled plan with an orphaned announcement");
                var orphanBaseline=memory.Bytes.ToDictionary(x=>x.Key,x=>x.Value);
                Throws(()=>snapshot.Plan(new DateTime(2026,7,27),new DateTime(2028,6,30)),"cannot invent a retirement date for an orphaned announcement");
                plan=snapshot.Plan(new DateTime(2026,7,27),null);
                Check(RetirementBatch.Apply(memory.Read,memory.Write,plan,()=>true,out error),"repair a legacy orphan without a retirement record");
                after=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());
                Check(after.Record==null && !after.HasAnnouncement && after.Records.Count==2,"repair leaves all other retirement plans intact");
                Check(orphanBaseline.All(kv=>kv.Key==person+0x18 || memory.Bytes[kv.Key]==kv.Value),"legacy repair touches no other game field");
                Throws(()=>after.Plan(new DateTime(2026,7,27),null),"normal players have no retirement cancellation to apply");
                reverse=plan.AsEnumerable().Reverse().Select(w=>new RetirementWrite{Address=w.Address,Old=w.New,New=w.Old,What="undo"}).ToList();
                Check(RetirementBatch.Apply(memory.Read,memory.Write,reverse,()=>true,out error),"legacy repair undo");
                Check(orphanBaseline.All(kv=>memory.Bytes[kv.Key]==kv.Value),"repair undo restores the exact orphan state without recreating a plan");
                memory=Fixture();snapshot=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());plan=snapshot.Plan(new DateTime(2026,7,27),null);
                memory.Put(person+0x18,new byte[]{0xA9});Check(!snapshot.StillCurrent(memory.Read),"changed person flags invalidate an open retirement dialog");
                int writesBefore=memory.Writes;
                Check(!RetirementBatch.Apply(memory.Read,memory.Write,plan,()=>true,out error) && memory.Writes==writesBefore,"reject changed flags before modifying either retirement index");
                memory=Fixture();memory.Put(person+0x18,new byte[]{0xAC});snapshot=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());
                plan=snapshot.Plan(new DateTime(2026,7,27),null);Check(RetirementBatch.Apply(memory.Read,memory.Write,plan,()=>true,out error),"cancel a record even if its announcement is already clear");
                after=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());Check(after.Record==null && !after.HasAnnouncement,"record-only cancellation remains supported");
                memory=Fixture();snapshot=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());
                Throws(()=>snapshot.Plan(new DateTime(2027,6,30),null),"no revival after retirement has taken effect");
                memory.Bytes.Remove(person+0x18);Throws(()=>RetirementSnapshot.Capture(memory.Read,manager,vt,Player()),"unreadable person announcement keeps retirement read-only");
                memory=Fixture();snapshot=RetirementSnapshot.Capture(memory.Read,manager,vt,Player());memory.Put(person+12,BitConverter.GetBytes(999));Check(!snapshot.StillCurrent(memory.Read),"reject stale person");
                plan=snapshot.Plan(new DateTime(2026,7,27),null);Check(!RetirementBatch.Apply(memory.Read,memory.Write,plan,()=>false,out error) && memory.Writes==0,"identity guard before all writes");
                Throws(()=>snapshot.Plan(new DateTime(2026,7,27),new DateTime(2027,1,1)),"reject bringing retirement forward");
                Check(RetirementSnapshot.VtableFor("27.0.2+changed")==0,"unknown game build read-only");
                var man=new byte[0xA0];man[0x19]=0x82;var woman=(byte[])man.Clone();woman[0x19]=0x92;
                Check(GenderFilter.Female(man,"27.0.1+2494687")==false,"male flag with unrelated flags");
                Check(GenderFilter.Female(woman,"27.0.1+2494687")==true,"female flag with unrelated flags");
                Check(GenderFilter.Female(woman,"27.0.2+changed")==null,"unknown gender mapping is not guessed");
                Check(GenderFilter.Female(new byte[4],"27.0.1+2494687")==null,"truncated person record");
                Check(PlayerNames.MatchRank("Cristiano Ronaldo|C罗","C罗")<PlayerNames.MatchRank("Cristian Romero|C·罗梅罗","C罗"),"exact aliases rank before partial names");
                DateTime gameDay=new DateTime(2026,7,28),birthday=new DateTime(1985,2,5);
                Check(Birthdays.AgeOn(birthday,gameDay)==41,"age uses the game date");
                Check(Birthdays.WithAge(birthday,gameDay,25)==new DateTime(2001,2,5),"preserve month/day after birthday");
                Check(Birthdays.WithAge(new DateTime(1990,12,10),gameDay,25)==new DateTime(2000,12,10),"preserve month/day before birthday");
                foreach(var day in new[]{new DateTime(2026,2,28),new DateTime(2028,2,28),new DateTime(2028,2,29),new DateTime(2028,3,1),new DateTime(2026,12,31)})
                    for(int age=1;age<=120;age++)Check(Birthdays.AgeOn(Birthdays.WithAge(new DateTime(2000,2,29),day,age),day)==age,"exact leap-day age conversion");
                uint packedBirth=((uint)1985<<16)|0x1A00u|36;
                uint changedBirth=Birthdays.Pack(packedBirth,new DateTime(2001,2,5),gameDay);
                Check(Birthdays.Decode(changedBirth)==new DateTime(2001,2,5),"birthday round trip");
                Check((changedBirth&0xFE00)==0x1A00,"preserve birthday flags");
                Throws(()=>Birthdays.Pack(packedBirth,new DateTime(2027,1,1),gameDay),"reject future birthday");
                Throws(()=>Birthdays.Decode(((uint)2025<<16)|366u),"reject invalid non-leap birthday");
                memory=new Memory();memory.Put(0x80000,BitConverter.GetBytes(packedBirth));var birthdayPlan=new System.Collections.Generic.List<RetirementWrite>{new RetirementWrite{Address=0x80000,Old=BitConverter.GetBytes(packedBirth),New=BitConverter.GetBytes(changedBirth),What="birthday"}};
                Check(RetirementBatch.Apply(memory.Read,memory.Write,birthdayPlan,()=>true,out error),"guarded birthday write");
                Check(Birthdays.AgeOn(Birthdays.Decode(BitConverter.ToUInt32(memory.Read(0x80000,4),0)),gameDay)==25,"read back changed age");
                birthdayPlan[0]=new RetirementWrite{Address=0x80000,Old=BitConverter.GetBytes(changedBirth),New=BitConverter.GetBytes(packedBirth),What="undo birthday"};
                Check(RetirementBatch.Apply(memory.Read,memory.Write,birthdayPlan,()=>true,out error),"guarded birthday undo");
                Check(memory.Read(0x80000,4).SequenceEqual(BitConverter.GetBytes(packedBirth)),"birthday undo restores all flags");
                PlayerNames.ApplyOfficial(new[]{p});p.Born=new DateTime(2001,2,5);string temp=Path.Combine(Path.GetTempPath(),"FM27E0-name-test-"+Guid.NewGuid().ToString("N"));
                PlayerNames.RememberBirthday(p,temp);PlayerNames.Load(temp);PlayerNames.ApplyOfficial(new[]{p});Check(p.ChineseName=="克里斯蒂亚诺·罗纳尔多","age edits retain verified bilingual identity on rescan");
                File.AppendAllText(Path.Combine(temp,"names","variants.tsv"),"1900000000\tOfficial Test Player\t2000-01-01\t测试球员\t甲方别名\thttps://sports.cctv.com/one\n1900000000\tOfficial Test Player\t2000-01-01\t测试球员\t乙方别名\thttps://sports.cctv.com/two\n1900000000\tOfficial Test Player\t2000-01-01\t另一译名\t丙方别名\thttps://www.news.cn/three\n",Encoding.UTF8);
                PlayerNames.Load(temp);var variant=new Player {Uid=1900000000,Name="Official Test Player",Search="Official Test Player",Born=new DateTime(2000,1,1)};PlayerNames.ApplyOfficial(new[]{variant});
                Check(variant.Matches("甲方别名") && variant.Matches("乙方别名") && variant.Matches("另一译名"),"multiple official records retain every useful alias");
                Check(new Uri(variant.NameSource).Host=="sports.cctv.com" && PlayerNames.Description(variant).Contains("sports.cctv.com"),"merging sources retains a valid preferred source URL");
                var chinaPlayer=new Player {Uid=1900000100,Name="China League Test Player",Search="China League Test Player",Born=new DateTime(2000,1,1)};
                PlayerNames.ApplyOfficial(new[]{chinaPlayer});Check(chinaPlayer.ChineseName==null && chinaPlayer.Matches("China League") && chinaPlayer.DisplayName==chinaPlayer.Name,"an uncatalogued Chinese-league player remains visible and searchable by the original name");
                var goodScan=new ScanResult();for(int i=0;i<1200;i++)goodScan.Players.Add(new Player {Uid=i+1,Name="Test Player "+i,Born=new DateTime(2000,1,1)});
                Check(Calibration.Verify(null,goodScan)==null,"zero clubs, staff, cities or manager records cannot invalidate player identification");
                int scans=0,calibrations=0;var notes=new List<string>();
                var recovered=ScanRecovery.Read(()=>{scans++;return goodScan;},()=>{calibrations++;throw new Exception("must not calibrate for absent clubs");},false,notes,s=>{});
                Check(recovered==goodScan && scans==1 && calibrations==0,"preserve a valid player list without a club or league dependency");
                var badScan=new ScanResult();ulong oldPersonType=FmProcess.PersonVtRva;
                try
                {
                    scans=0;notes.Clear();
                    recovered=ScanRecovery.Read(()=>++scans==1?goodScan:badScan,()=>{FmProcess.PersonVtRva=0x12345;return new List<string>();},true,notes,s=>{});
                    Check(recovered==goodScan && FmProcess.PersonVtRva==oldPersonType,"failed forced recovery restores both valid players and the original mapping");
                    Check(notes.Any(s=>s.Contains("已恢复")),"report a recovery rollback accurately");
                    scans=0;notes.Clear();
                    recovered=ScanRecovery.Read(()=>++scans==1?badScan:goodScan,()=>new List<string>(),false,notes,s=>{});
                    Check(recovered==goodScan && scans==2 && notes.Last().Contains("通过"),"successful player recovery is confirmed by a new scan");
                    notes.Clear();recovered=ScanRecovery.Read(()=>badScan,()=>new List<string>(),false,notes,s=>{});
                    Check(recovered==badScan && notes.Last().Contains("仍未通过"),"a zero-player recovery remains a failure");
                    recovered=ScanRecovery.Read(()=>goodScan,()=>{FmProcess.PersonVtRva=0x12346;throw new IOException("test failure");},true,notes,s=>{});
                    Check(recovered==goodScan && FmProcess.PersonVtRva==oldPersonType,"recovery exceptions retain a previously verified mapping");
                    Throws(()=>ScanRecovery.Read(()=>badScan,()=>{FmProcess.PersonVtRva=0x12347;throw new IOException("test failure");},false,notes,s=>{}),"errors without a valid initial list do not fabricate success");
                    Check(FmProcess.PersonVtRva==oldPersonType,"mapping is restored after a failed initial recovery");
                }
                finally {FmProcess.PersonVtRva=oldPersonType;}
                var badIdentities=new ScanResult();badIdentities.Players.AddRange(goodScan.Players.Select(x=>new Player{Name=x.Name}));
                Check(ScanRecovery.PlayerIssue(badIdentities)!=null,"recovery still rejects a large list with invalid identities");
                var blocked=new Dictionary<string,string>{{"core","0 名球员"}};
                string attach=AttachReport.Build("27.0.1+2494687",false,badScan,blocked,false);
                Check(attach.Contains("全部编辑功能保持只读") && attach.Contains("未收录中国联赛") && !attach.Contains("已自动重新找到"),"zero-player report explains the failure without claiming calibration success");
                attach=AttachReport.Build("27.0.1+2494687",false,goodScan,new Dictionary<string,string>{{"status","not mapped"},{"injury","not mapped"},{"jaded","not mapped"}},true);
                Check(attach.Contains("球员核心数据检查通过") && attach.Contains("尚未适配") && !attach.Contains("全部编辑功能保持只读"),"partial support is clearly distinguished from a fully blocked editor");
                double money,exchange;
                foreach(var pair in new[]{Tuple.Create("人民币 1.5亿",150000000d),Tuple.Create("￥2000万",20000000d),Tuple.Create("$13,651,375",13651375d),Tuple.Create("£2.5m",2500000d),Tuple.Create("1.2b",1200000000d),Tuple.Create("（人民币2.5万）",-25000d),Tuple.Create("-1,200.50",-1200.5d),Tuple.Create(".5k",500d)})
                    Check(CurrencySetting.TryMoney(pair.Item1,out money) && money==pair.Item2,"money parser preserves currency magnitude: "+pair.Item1);
                foreach(var invalid in new[]{"","NaN","Infinity","1e400","20万元亿","12,34","5abc","--1"})Check(!CurrencySetting.TryMoney(invalid,out money),"reject malformed money: "+invalid);
                foreach(var invalid in new[]{"","NaN","Infinity","1e400","0","-1","八"})Check(!CurrencySetting.TryRate(invalid,out exchange),"reject invalid conversion rate: "+invalid);
                Check(CurrencySetting.TryRate("8,5",out exchange) && exchange==8.5,"comma-decimal conversion rates");
                Check(CurrencySetting.FromBalance("1.5亿",10000000)==15 && CurrencySetting.FromBalance("-1.5亿",-10000000)==15,"optional balance calibration supports Chinese units and negative balances");
                Throws(()=>CurrencySetting.FromBalance("100",0),"zero club balance does not define an exchange rate");
                Throws(()=>CurrencySetting.FromBalance("-100",100),"opposite balance signs cannot define an exchange rate");
                var currency=CurrencySetting.Read(new[]{"symbol=$","rate=1.3476"},new CurrencySetting("£",1));
                Check(currency.Symbol=="$" && currency.Rate==1.3476,"migrate existing currency settings");
                currency=CurrencySetting.Read(new[]{"symbol=¥","rate=NaN"},currency);Check(currency.Symbol=="£" && currency.Rate==1,"corrupt settings do not attach an invalid rate to a currency");
                Check(new CurrencySetting("£",88).Rate==1,"the stored base currency always has a one-to-one rate");
                string currencyPath=Path.Combine(temp,"currency-settings.txt");currency=new CurrencySetting("¥",1e-9);currency.Save(currencyPath);
                var reloaded=CurrencySetting.Read(File.ReadAllLines(currencyPath),new CurrencySetting("£",1));
                Check(reloaded.Symbol=="¥" && reloaded.Rate==currency.Rate,"settings retain symbol and full rate precision after restart");
                Throws(()=>currency.Save(Path.Combine(temp,"missing-directory","settings.txt")),"saving errors are reported instead of silently succeeding");
                using(var form=new MainForm(false,false))
                using(var dialog=form.BuildCurrencyDialog())
                {
                    var symbols=(System.Windows.Forms.ComboBox)dialog.Controls.Find("CurrencySymbol",true).Single();
                    var rate=(System.Windows.Forms.TextBox)dialog.Controls.Find("CurrencyRate",true).Single();
                    var manual=(System.Windows.Forms.RadioButton)dialog.Controls.Find("ManualCurrency",true).Single();
                    var byBalance=(System.Windows.Forms.RadioButton)dialog.Controls.Find("BalanceCalibration",true).Single();
                    Check(symbols.Enabled && manual.Checked && !byBalance.Enabled,"currency can be configured without attaching to a game or finding a club");
                    symbols.SelectedIndex=0;Check(rate.Text=="" && rate.Enabled,"changing to RMB requires its own explicit conversion rate");
                    symbols.SelectedIndex=1;Check(rate.Text=="1" && !rate.Enabled,"pound selection works without club calibration");
                }
                Check(p.Matches("735216") && p.Matches("#735216") && p.Matches("UID:735216") && !p.Matches("735"),"exact player UID search is independent of translations");
                int uid;Check(PlayerNames.TryUid("uid：735216",out uid) && uid==735216,"UID prefixes support Chinese punctuation");
                Check(!PlayerNames.TryUid("-1",out uid) && !PlayerNames.TryUid("9999999999999",out uid),"invalid UIDs cannot overflow or become other identities");
                Check(PlayerNames.TryUid("＃７３５２１６",out uid) && uid==735216,"full-width UID input from Chinese keyboards");
                var staffUid=new Staff {Uid=12345,Name="Test Coach",Search="Test Coach"};
                Check(staffUid.Matches("#12345") && staffUid.Matches("Test Coach") && !staffUid.Matches("1234"),"staff name and UID searches coexist");
                using(var command=new System.Windows.Forms.Button {Text="test command"})
                using(var group=MainForm.MenuGroup("test group",command))
                {
                    int fired=0;command.Click+=delegate {fired++;};
                    ((System.Windows.Forms.ToolStripMenuItem)group.DropDownItems[0]).PerformClick();
                    Check(fired==1,"advanced menu dispatches the existing command handler");
                    command.Enabled=false;MainForm.RefreshCommandMenu(group.DropDownItems);
                    ((System.Windows.Forms.ToolStripMenuItem)group.DropDownItems[0]).PerformClick();
                    Check(fired==1,"disabled advanced commands cannot invoke their handlers");
                }
                using(var form=new MainForm(false,false))
                {
                    var tabs=(System.Windows.Forms.TabControl)form.Controls.Find("WorkspaceTabs",true).Single();
                    var navigation=(System.Windows.Forms.FlowLayoutPanel)form.Controls.Find("WorkspaceNavigation",true).Single();
                    Check(tabs.TabPages.Count==10 && navigation.Controls.OfType<System.Windows.Forms.Button>().Count()==10,"grouped navigation preserves all ten feature pages");
                    foreach(var button in navigation.Controls.OfType<System.Windows.Forms.Button>())
                    {
                        typeof(System.Windows.Forms.Button).GetMethod("OnClick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(button,new object[]{EventArgs.Empty});
                        Check(tabs.SelectedIndex==(int)button.Tag,"navigation and the original page indices stay synchronized");
                    }
                    tabs.SelectedIndex=0;
                    var bar=(System.Windows.Forms.FlowLayoutPanel)form.Controls.Find("PlayerActions",true).Single();
                    Check(bar.Controls.OfType<System.Windows.Forms.Button>().Count()==6 && bar.Controls.Find("PlayerOptimizeAttributes",true).Length==1,"player toolbar adds an independent optimizer beside common actions and the advanced menu");
                    var more=(System.Windows.Forms.Button)bar.Controls.Find("MorePlayerActions",true).Single();
                    var groups=more.ContextMenuStrip.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().Where(x=>x.HasDropDownItems).ToArray();
                    Check(groups.Length==3 && groups.Sum(g=>g.DropDownItems.Count)==10,"advanced commands including registration remain available in categorized menus");
                    var item=(System.Windows.Forms.ToolStripMenuItem)groups[0].DropDownItems[0];var original=(System.Windows.Forms.Button)item.Tag;
                    original.Enabled=false;MainForm.RefreshCommandMenu(more.ContextMenuStrip.Items);Check(!item.Enabled,"menu commands respect existing feature safety states");
                    original.Enabled=true;MainForm.RefreshCommandMenu(more.ContextMenuStrip.Items);Check(item.Enabled,"menu availability refreshes after feature checks change");
                    Check(form.Controls.Find("ScanDetailsButton",true).Length==1 && form.Controls.Find("NameCatalogButton",true).Length==1,"diagnostics and name coverage remain accessible");
                    Check(form.Controls.Find("LeagueRegistrationButton",true).Length==1 && tabs.TabPages[7].Text=="赛事与报名","competition rules are accessible without selecting a player or a prize-money table");
                    foreach(var size in new[]{new System.Drawing.Size(1120,720),new System.Drawing.Size(1360,860)})
                    {
                        form.Size=size;form.PerformLayout();
                        Check(navigation.Width>=150 && tabs.Width>600,"navigation leaves usable editing space at supported sizes");
                    }
                }
                EligibilityTests.Run(Check);
                EligibilityTests.RunCountryCases(Check);
                NameFallbackTests.Run(Check);
                ScanDiagnosticsTests.Run(Check);
                CurrentBuildTests.Run(Check);
                CurrentFeaturesTests.Run(Check);
                GrowthRoleCatalogTests.Run(Check);
                GrowthFocusTests.Run(Check);
                GrowthSelectionTests.Run(Check);
                GrowthMultiplierTests.Run(Check);
                GrowthSupportTests.Run(Check);
                GrowthLimitTests.Run(Check);
                GrowthSquadTests.Run(Check);
                AttributeOptimizerTests.Run(Check);
                PositionAbilityTests.Run(Check);
                EarlySaveTests.Run(Check);
                DefaultPlayerDatabaseTests.Run(Check);
                ClubFacilitiesTests.Run(Check);
                NativeEngineTests.Run(Check);
                ClubCatalogTests.Run(Check);
                ClubFinanceTests.Run(Check);
                RosterRefreshTests.Run(Check);
                PerformanceMaintenanceTests.Run(Check);
                WorkspaceLayoutTests.Run(Check);
                StaffTransferTests.Run(Check);
                Console.WriteLine("PASS: "+assertions+" assertions; patched club discovery, cross-club queues, squads/loans, names and guarded editing/undo.");return 0;
            }
            catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
        }
    }
}
