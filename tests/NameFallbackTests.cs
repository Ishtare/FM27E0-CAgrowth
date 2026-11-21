using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using FMPersonChinese;

namespace Fm24Editor
{
    static class NameFallbackTests
    {
        static Player Person(string first,string last,int nation,int uid=1509000000,string common=null)
        {
            string original=common??(first+" "+last).Trim();
            return new Player {Uid=uid,Name=original,Search=original,NameNationUid=nation,NameParts=new PersonNameParts {First=first,Last=last,Common=common,Search=original}};
        }
        public static void Run(Action<bool,string> check)
        {
            NameFallback.Initialize();
            check(NameFallback.LoadNote.Length==0 && NameFallback.WordRows==506169 && NameFallback.ContextRows==91482,"complete embedded name bank loads without another chat's files or a plugin");
            var examples=new[]{Person("Achille","Cauli",776),Person("Achille","Tigano",776),Person("Alessandro","Grigioni",776),Person("Coran","Madden",762),Person("Davide","Perna",776),Person("Davide","Zamagni",776),Person("Denis","Nikolli",776),Person("Diego","Martinez",796),Person("Domenico","Molteni",776),Person("Edoardo","Franzosi",776),Person("Ettore","Broggian",776),Person("Filippo","Briccola",776),Person("Francesco Luis","Andrealli",776),Person("Gabriele","Farsaci",776),Person("Gabriele","Gardanini",776),Person("Ilias","Khouabba",10),Person("Jayden","Marshall",765),Person("Josh","Harpur",762),Person("Kevin","Fustini",776)};
            foreach(var p in examples)
            {
                string original=p.Name,search=p.Search;PlayerNames.Apply(new[]{p});
                check(p.ChineseName!=null && NameDictionary.IsChineseName(p.ChineseName),"screenshot and youth names receive Chinese: "+original);
                check(p.Name==original && p.Search==search && p.Matches(original) && p.Matches(p.ChineseName),"Chinese and English search share the same original person: "+original);
                check(p.FallbackName!=null && p.NameSource==null && p.DisplayName.Contains(original),"component/automatic results keep their origin and English display: "+original);
            }
            var marcos=Person("Marcos","Herrera",796);PlayerNames.Apply(new[]{marcos});
            check(marcos.ChineseName=="马科斯·赫雷拉" && !marcos.FallbackName.Automatic,"official language-specific components retain their established spellings");
            var invented=Person("Zorquendavio","Nuvandellox",796);PlayerNames.Apply(new[]{invented});
            check(invented.FallbackName.Automatic && invented.DisplayName.Contains("〔音译〕") && PlayerNames.Description(invented).Contains("近似"),"unseen generated names are explicitly approximate");
            var firstResult=invented.ChineseName;PlayerNames.Apply(new[]{invented});check(invented.ChineseName==firstResult,"repeated generated-name translation is stable");
            var nickname=Person("James","Anderson",765,1509000010,"Zorquendavio");PlayerNames.Apply(new[]{nickname});
            check(nickname.ChineseName!=null && nickname.FallbackName.Automatic && !nickname.ChineseName.Contains("詹姆斯"),"a common name takes precedence over unrelated first/surname composition");
            var existing=Person("","张伟",110);PlayerNames.Apply(new[]{existing});
            check(existing.DisplayName=="张伟" && existing.FallbackName.Source==FMPersonChinese.NameSource.PreservedChinese,"existing Chinese is retained without a duplicate bilingual label");
            var unknown=Person("Zorquendavio","Nuvandellox",-1);PlayerNames.Apply(new[]{unknown});
            check(unknown.FallbackName!=null && unknown.FallbackName.LowConfidence,"unknown language is marked uncertain");
            var messi=Person("Lionel","Messi",164,7458500);messi.Born=new DateTime(2008,6,24);PlayerNames.Apply(new[]{messi});
            check(messi.ChineseName=="梅西" && messi.FallbackName==null && messi.NameSource.StartsWith("http"),"verified person aliases retain priority over general component guesses and age edits");
            var diao=Person("Assane","Diao",769,2000210791);diao.Born=new DateTime(2005,9,7);PlayerNames.Apply(new[]{diao});
            check(NameDictionary.IsChineseName(diao.ChineseName) && diao.Matches("A·迪奥") && diao.Matches("Assane Diao"),"published full Chinese names replace Latin-initial display while retaining old abbreviation search");
            var changed=Person("Achille","Cauli",776);PlayerNames.Apply(new[]{changed});string oldChinese=changed.ChineseName;
            changed.Name=changed.Search="Zorquendavio Nuvandellox";PlayerNames.Apply(new[]{changed});
            check(changed.ChineseName!=oldChinese && changed.FallbackName.Automatic,"changing original names invalidates stale component metadata and cached display");
            var single=Person("","梅",110);PlayerNames.Apply(new[]{single});
            check(PlayerNames.CanSearchPlayer("梅") && single.Matches("梅") && !messi.Matches("梅"),"a displayed one-character name is searchable without matching surname prefixes");
            var cached=Person("John","Example",765);cached.NameAliases="旧名";check(cached.Matches("旧名"),"search cache starts with current aliases");
            cached.NameAliases="新名";check(cached.Matches("新名") && !cached.Matches("旧名"),"search cache invalidates when aliases change");
            cached.ChineseName="新中文";check(cached.Matches("新中文"),"direct Chinese display changes invalidate search keys");
            var bank=typeof(NameFallback).GetField("dictionary",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null) as NameDictionary;
            check(bank.TranslatePart(NameKind.First,"Olivier",769).Chinese=="奥利弗" && bank.TranslatePart(NameKind.Surname,"Olivier",769).Chinese=="奥利维尔","first-name and surname components remain distinct");
            check(bank.TranslatePart(NameKind.First," ",765)==null && bank.TranslatePart(NameKind.First,new string('A',257),765)==null,"empty and overlong names do not produce fabricated entries");
            foreach(string language in new[]{"en","fr","de","es","it","pt-br","ru","ar","ja","ko","cmn","tr","pl"})
            {
                var engine=new PhoneticEngine();var result=engine.Convert("Zorquendavio",language);
                check(result!=null && NameDictionary.IsChineseName(result.Chinese) && result.LowConfidence,"managed language rule supplies a labelled approximation: "+language);
            }
            foreach(string script in new[]{"Алексей","Δημήτρης","محمد","יוסף","たろう","민준","नवीन","გიორგი"})
            {
                var result=new PhoneticEngine().Convert(script,"en");
                check(result!=null && NameDictionary.IsChineseName(result.Chinese),"non-Latin script produces a Chinese candidate: "+script);
            }
            var imports=typeof(PhoneticEngine).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance).Where(m=>m.GetCustomAttributes(typeof(DllImportAttribute),false).Length>0);
            check(!imports.Any(),"phonetic rules contain no native speech-library imports");
            var coach=new Staff {Uid=1519000000,Name="Alessandro Grigioni",Search="Alessandro Grigioni | Original Club",NameNationUid=776,NameParts=new PersonNameParts {First="Alessandro",Last="Grigioni"}};
            PlayerNames.ApplyStaff(new[]{coach});
            check(coach.NameView.ChineseName!=null && coach.Matches(coach.NameView.ChineseName) && coach.Matches("Alessandro") && coach.ToString().Contains(coach.NameView.ChineseName),"staff lists, person lookup and Chinese/English search use the same translation rules");
            coach.Search+=" | New Club";check(coach.Matches("New Club") && coach.Matches(coach.NameView.ChineseName),"staff club-search updates retain translated-name matching");
            coach.Name=coach.Search="Different Coach";check(coach.DisplayName=="Different Coach" && !coach.Matches("Grigioni"),"renaming a staff member cannot display stale translated identity");
            for(int batch=0;batch<3;batch++)
            {
                var future=new List<Player>();
                for(int i=0;i<30;i++)future.Add(Person("Zorq"+i+"navio","Nuv"+batch+"dellox",new[]{765,796,771,769,776,788}[i%6],1510000000+batch*100+i));
                PlayerNames.Apply(future);
                check(future.All(p=>p.FallbackName!=null && p.FallbackName.Automatic && NameDictionary.IsChineseName(p.ChineseName) && p.Matches(p.ChineseName) && p.Matches(p.Name)),"new unseen batch receives Chinese and bilingual search without new source records: "+batch);
            }
        }
    }
}
