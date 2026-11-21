// Modified build notice: FM27E0-CAgrowth v1.1.12; changes dated 2026-11-15.
// GPL-3.0-or-later. Reuses the workspace's managed name rules; see NAME-FALLBACK-NOTICE.txt.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FMPersonChinese;

namespace Fm24Editor
{
    sealed class PersonNameParts
    {
        public string First,Last,Common,Full,Search;
    }
    sealed class NameFallbackResult
    {
        public string Chinese,Language,Note;
        public FMPersonChinese.NameSource Source;
        public bool LowConfidence;
        public bool Automatic {get{return Source==FMPersonChinese.NameSource.Automatic || Source==FMPersonChinese.NameSource.Mixed || Source==FMPersonChinese.NameSource.MixedExisting;}}
        public string Label
        {
            get
            {
                switch(Source)
                {
                    case FMPersonChinese.NameSource.OfficialFull:return "游戏词库完整译名";
                    case FMPersonChinese.NameSource.OfficialComponents:return "游戏姓名词条组合";
                    case FMPersonChinese.NameSource.Automatic:return "自动音译";
                    case FMPersonChinese.NameSource.Mixed:return "词条与自动音译组合";
                    case FMPersonChinese.NameSource.MixedExisting:return "原有中文与音译组合";
                    default:return "游戏原有中文";
                }
            }
        }
        public string Badge {get{return Automatic?"〔音译〕":Source==FMPersonChinese.NameSource.OfficialComponents?"〔组合〕":Source==FMPersonChinese.NameSource.OfficialFull?"〔词库〕":"";}}
    }
    static class NameFallback
    {
        static readonly object gate=new object();
        static readonly PhoneticEngine engine=new PhoneticEngine();
        static NameDictionary dictionary;
        static bool attempted;
        public static string LoadNote="";
        public static long LoadMilliseconds;
        public static int WordRows {get{return dictionary==null?0:dictionary.WordRows;}}
        public static int ContextRows {get{return dictionary==null?0:dictionary.ContextRows;}}
        static IEnumerable<string> Section(string file)
        {
            string wanted=file=="countries.tsv"?"[countries]":file=="words.tsv"?"[words]":"[contexts]";
            using(var raw=typeof(NameFallback).Assembly.GetManifestResourceStream("FM27E0.name-bank.gz"))
            {
                if(raw==null)throw new InvalidDataException("内置完整姓名词库未找到。");
                using(var zip=new GZipStream(raw,CompressionMode.Decompress))
                using(var reader=new StreamReader(zip,Encoding.UTF8,true))
                {
                    if(reader.ReadLine()!="FMEDITOR-NAMES-1")throw new InvalidDataException("姓名词库格式不匹配。");
                    string line;bool active=false;long size=0;
                    while((line=reader.ReadLine())!=null)
                    {
                        size+=line.Length;
                        if(size>32000000 || line.Length>4096)throw new InvalidDataException("姓名词库内容超出范围。");
                        if(line.StartsWith("[",StringComparison.Ordinal))
                        {if(active)yield break;active=line==wanted;continue;}
                        if(active)yield return line;
                    }
                    if(!active)throw new InvalidDataException("姓名词库缺少所需资料表。");
                }
            }
        }
        public static void Initialize()
        {
            lock(gate)
            {
                if(attempted)return;attempted=true;var watch=Stopwatch.StartNew();
                try
                {
                    dictionary=new NameDictionary(Section,engine,100000);
                    if(dictionary.WordRows!=506169 || dictionary.ContextRows!=91482)throw new InvalidDataException("完整姓名词库条目数不符。");
                }
                catch(Exception ex){dictionary=null;LoadNote="完整姓名词库未载入，暂用已有对照及自动音译："+ex.Message;}
                finally{watch.Stop();LoadMilliseconds=watch.ElapsedMilliseconds;}
            }
        }
        public static NameFallbackResult Translate(Player player)
        {
            if(string.IsNullOrWhiteSpace(player.Name) || player.Name.Length>256)return null;
            Initialize();
            if(NameDictionary.IsChineseName(player.Name))
                return new NameFallbackResult {Chinese=player.Name,Source=FMPersonChinese.NameSource.PreservedChinese,Note="保留游戏原有中文，不推定其来源。"};
            string first=null,last=null,common=null;
            if(player.NameParts!=null && player.NameParts.Search==player.Search)
            {first=player.NameParts.First;last=player.NameParts.Last;common=player.NameParts.Common;}
            else
            {
                var parts=player.Name.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
                if(parts.Length>1){first=parts[0];last=string.Join(" ",parts.Skip(1).ToArray());}
                else common=player.Name;
            }
            NameTranslation translated=null;
            try{if(dictionary!=null)translated=dictionary.TranslateDisplay(first,last,common,player.NameNationUid,player.Uid);}
            catch(Exception){ /* One malformed name must not prevent other people from being displayed. */ }
            if(translated==null || !NameDictionary.IsChineseName(translated.Chinese))
            {
                string language=dictionary==null?"en":dictionary.Country(player.NameNationUid).Language;
                var guess=engine.Convert(player.Name,language);
                if(guess==null || !NameDictionary.IsChineseName(guess.Chinese))return null;
                translated=new NameTranslation {Chinese=guess.Chinese,Language=guess.Language,Source=FMPersonChinese.NameSource.Automatic,LowConfidence=true,Note="automatic managed rule"};
            }
            return new NameFallbackResult {Chinese=translated.Chinese,Language=translated.Language,Source=translated.Source,LowConfidence=translated.LowConfidence,Note=translated.Note};
        }
        public static string Description(NameFallbackResult result)
        {
            string origin=result.Automatic?"这是近似音译，可能与惯用译名不同。":result.Source==FMPersonChinese.NameSource.PreservedChinese?"保留游戏原有中文，未推定其官方来源。":result.Source==FMPersonChinese.NameSource.OfficialFull?"按原名和词条的身份、地区条件匹配。":"姓名组成词条的组合不等于已核对该人物的官方全名。";
            return result.Label+"；"+origin+
                " 保留英文和中文搜索，仅影响修改器显示。"+(result.LowConfidence?"语言或读音存在不确定性。":"");
        }
    }
}
