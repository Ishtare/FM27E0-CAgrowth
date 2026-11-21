// Modified build notice: FM27E0-CAgrowth v1.1.12; changes dated 2026-11-15.
// Adapted from this workspace's FMPersonChinese 0.2.0 authored GPL-3.0-or-later source.
// Source reference and data ownership: docs/name-translations.md. No native bridge is included.
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace FMPersonChinese {
 public enum NameKind { First, Surname, Common }
 public enum NameSource { OfficialFull, OfficialComponents, Automatic, Mixed, PreservedChinese, MixedExisting }
 public sealed class NameTranslation {
  public string Chinese,Language,Note;
  public NameSource Source;
  public bool LowConfidence;
 }
 public sealed class CountryInfo {
  public int Id,Ordinal;
  public string Name,Language;
  public bool LanguageApproximate;
 }
 public sealed class PhoneticResult {
  public string Chinese,Language,Ipa;
  public bool LowConfidence;
 }
 public interface IPhonetics { PhoneticResult Convert(string original,string language); }

 public sealed class NameDictionary {
  sealed class Word { public string Chinese;public int Nation; }
  sealed class Context { public uint Person;public int Nation;public string Chinese; }
  readonly Dictionary<int,CountryInfo> countries=new Dictionary<int,CountryInfo>();
  readonly Dictionary<int,CountryInfo> countriesByOrdinal=new Dictionary<int,CountryInfo>();
  readonly Dictionary<string,List<Word>> exact=new Dictionary<string,List<Word>>(StringComparer.Ordinal);
  readonly Dictionary<string,List<Word>> folded=new Dictionary<string,List<Word>>(StringComparer.Ordinal);
  readonly Dictionary<string,List<Context>> contexts=new Dictionary<string,List<Context>>(StringComparer.Ordinal);
  readonly ConcurrentDictionary<string,NameTranslation> cache=new ConcurrentDictionary<string,NameTranslation>(StringComparer.Ordinal);
  readonly IPhonetics phonetics;
  public int WordRows {get;private set;}
  public int ContextRows {get;private set;}
  public int Cached {get{return cache.Count;}}
  static readonly Dictionary<string,string> Particles=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
   {"de","德"},{"da","达"},{"do","杜"},{"dos","多斯"},{"das","达斯"},{"di","迪"},{"du","迪"},{"del","德尔"},{"della","德拉"},{"la","拉"},{"le","勒"},{"van","范"},{"von","冯"},{"der","德"},{"den","登"},{"al","阿尔"},{"el","埃尔"},{"bin","本"},{"ibn","伊本"}
  };
  public NameDictionary(string directory,IPhonetics engine):this(name=>File.ReadLines(Path.Combine(directory,name),Encoding.UTF8),engine,100000){}
  public NameDictionary(Func<string,IEnumerable<string>> lines,IPhonetics engine,int minimumWordRows){
   phonetics=engine;
   foreach(string line in lines("countries.tsv").Skip(1)){
    var p=line.Split('\t');if(p.Length!=5)throw new FormatException("Invalid country row");
    var c=new CountryInfo{Id=int.Parse(p[0],CultureInfo.InvariantCulture),Ordinal=int.Parse(p[1],CultureInfo.InvariantCulture),Name=p[2],Language=p[3],LanguageApproximate=p[4]=="1"};countries.Add(c.Id,c);countriesByOrdinal.Add(c.Ordinal,c);
   }
   foreach(string line in lines("words.tsv").Skip(1)){
    var p=line.Split('\t');if(p.Length!=4||!HasChinese(p[3]))throw new FormatException("Invalid official word row");
    string prefix=p[0]+"\u001f";var word=new Word{Nation=int.Parse(p[1],CultureInfo.InvariantCulture),Chinese=p[3]};
    AddWord(exact,prefix+Normalize(p[2],false),word);AddWord(folded,prefix+Normalize(p[2],true),word);WordRows++;
   }
   foreach(string line in lines("contexts.tsv").Skip(1)){
    var p=line.Split('\t');if(p.Length!=6)throw new FormatException("Invalid official context row");
    string key=ContextKey(p[2],p[3],p[4]);List<Context> list;
    if(!contexts.TryGetValue(key,out list)){list=new List<Context>();contexts.Add(key,list);}
    list.Add(new Context{Person=uint.Parse(p[0],CultureInfo.InvariantCulture),Nation=int.Parse(p[1],CultureInfo.InvariantCulture),Chinese=p[5]});ContextRows++;
   }
   if(WordRows<minimumWordRows||countries.Count<200)throw new FormatException("Official dictionaries are incomplete");
  }
  static void AddWord(Dictionary<string,List<Word>> map,string key,Word word){List<Word> list;if(!map.TryGetValue(key,out list)){list=new List<Word>();map.Add(key,list);}list.Add(word);}
  public CountryInfo Country(int id){CountryInfo c;return countries.TryGetValue(id,out c)?c:new CountryInfo{Id=id,Ordinal=-1,Name="Unknown",Language="en"};}
  public string CountryName(int id){return Country(id).Name;}
  public static bool HasChinese(string value){if(value==null)return false;foreach(char c in value)if(c>=0x3400&&c<=0x9fff)return true;return false;}
  public static bool HasForeignLetters(string value){if(value==null)return false;foreach(char c in value)if(char.IsLetter(c)&&!(c>=0x3400&&c<=0x9fff))return true;return false;}
  public static bool IsChineseName(string value){return HasChinese(value)&&!HasForeignLetters(value);}
  public static string Normalize(string input,bool accents){
   string s=(input??"").Trim().Normalize(accents?NormalizationForm.FormKD:NormalizationForm.FormKC).ToLowerInvariant();
   var b=new StringBuilder();bool space=false;
   foreach(char c in s){var kind=char.GetUnicodeCategory(c);if(accents&&(kind==UnicodeCategory.NonSpacingMark||kind==UnicodeCategory.SpacingCombiningMark))continue;
    if(char.IsWhiteSpace(c)){if(b.Length>0)space=true;continue;}if(space){b.Append(' ');space=false;}b.Append(c=='’'?'\'':c);}
   return b.ToString();
  }
  static string ContextKey(string first,string last,string common){return Normalize(first,false)+"\u001f"+Normalize(last,false)+"\u001f"+Normalize(common,false);}
  string OfficialWord(NameKind kind,string value,CountryInfo country,out string note,out bool uncertain){
   note="";uncertain=false;string prefix=kind==NameKind.First?"F\u001f":"S\u001f";
   var candidates=new List<Word>();List<Word> found;
   if(kind==NameKind.Common){if(exact.TryGetValue("F\u001f"+Normalize(value,false),out found))candidates.AddRange(found);if(exact.TryGetValue("S\u001f"+Normalize(value,false),out found))candidates.AddRange(found);}
   else if(exact.TryGetValue(prefix+Normalize(value,false),out found))candidates.AddRange(found);
   if(candidates.Count==0){
    if(kind==NameKind.Common){if(folded.TryGetValue("F\u001f"+Normalize(value,true),out found))candidates.AddRange(found);if(folded.TryGetValue("S\u001f"+Normalize(value,true),out found))candidates.AddRange(found);}
    else if(folded.TryGetValue(prefix+Normalize(value,true),out found))candidates.AddRange(found);
    if(candidates.Count>0)note="official spelling variant";
   }
   if(candidates.Count==0)return null;
   var local=candidates.Where(w=>w.Nation==country.Ordinal).Select(w=>w.Chinese).Distinct().ToArray();
   if(local.Length==1)return local[0];if(local.Length>1){uncertain=true;return null;}
   var global=candidates.Where(w=>w.Nation==65535).Select(w=>w.Chinese).Distinct().ToArray();if(global.Length==1){note="official generic entry";return global[0];}
   var sameLanguage=candidates.Where(w=>{CountryInfo c;return countriesByOrdinal.TryGetValue(w.Nation,out c)&&c.Language==country.Language;}).Select(w=>w.Chinese).Distinct().ToArray();
   if(sameLanguage.Length==1){note="official component from same language";return sameLanguage[0];}
   var all=candidates.Select(w=>w.Chinese).Distinct().ToArray();if(all.Length==1){note="unambiguous official component reused across origins";return all[0];}
   uncertain=true;return null;
  }
  public NameTranslation ExactContext(string first,string last,string common,int countryId,int personId){
   List<Context> values;if(!contexts.TryGetValue(ContextKey(first,last,common),out values))return null;
   var country=Country(countryId);
   var valid=values.Where(v=>(v.Nation==country.Ordinal||v.Nation==65535)&&(v.Person==uint.MaxValue||(personId>=0&&v.Person==(uint)personId))).ToArray();
   if(valid.Length==0)return null;
   var specific=valid.Where(v=>v.Person!=uint.MaxValue).ToArray();if(specific.Length>0)valid=specific;
   var translations=valid.Select(v=>v.Chinese).Distinct().ToArray();if(translations.Length!=1)return null;
   return new NameTranslation{Chinese=translations[0],Language=country.Language,Source=specific.Length>0?NameSource.OfficialFull:NameSource.OfficialComponents,Note=specific.Length>0?"official entity-specific translation":"official contextual name entry"};
  }
  public NameTranslation TranslatePart(NameKind kind,string original,int countryId){
   if(string.IsNullOrWhiteSpace(original))return null;
   original=original.Trim();if(original.Length>256||original.IndexOf('\0')>=0)return null;
   if(IsChineseName(original))return new NameTranslation{Chinese=original,Source=NameSource.PreservedChinese,Language=Country(countryId).Language,Note="existing Chinese kept"};
   string key=((int)kind).ToString()+"|"+countryId.ToString(CultureInfo.InvariantCulture)+"|"+Normalize(original,false);
   NameTranslation cached;if(cache.TryGetValue(key,out cached))return cached;
   var result=TranslateUncached(kind,original,Country(countryId));
   if(result!=null&&IsChineseName(result.Chinese))cache.TryAdd(key,result);
   return result;
  }
  NameTranslation TranslateUncached(NameKind kind,string original,CountryInfo country){
   bool uncertain;string note;
   string translated=OfficialWord(kind,original,country,out note,out uncertain);
   if(translated!=null&&IsChineseName(translated))return new NameTranslation{Chinese=translated,Source=NameSource.OfficialComponents,Language=country.Language,Note=note};
   var pieces=SplitName(original);bool multiple=pieces.Count>1;
   if(multiple){
    var output=new StringBuilder();var parts=new List<NameTranslation>();
    foreach(var piece in pieces){
     if(piece=="-"||piece=="·"){if(output.Length>0&&output[output.Length-1]!='·'&&output[output.Length-1]!='-')output.Append(piece);continue;}
     var part=TranslatePart(kind,piece,country.Id);if(part==null)return null;
     if(output.Length>0&&output[output.Length-1]!='·'&&output[output.Length-1]!='-')output.Append('·');output.Append(part.Chinese);parts.Add(part);
    }
    if(parts.Count>0)return Combine(output.ToString(),parts,country.Language,"compound name components");
   }
   if(HasChinese(original)&&HasForeignLetters(original)){
    var output=new StringBuilder();var foreign=new StringBuilder();var parts=new List<NameTranslation>();
    Action flush=()=>{if(foreign.Length==0)return;var r=TranslatePart(kind,foreign.ToString(),country.Id);if(r!=null){output.Append(r.Chinese);parts.Add(r);}foreign.Clear();};
    foreach(char c in original){if(c>=0x3400&&c<=0x9fff){flush();output.Append(c);}else if(char.IsLetterOrDigit(c)||c=='\'')foreign.Append(c);else{flush();output.Append(c);}}flush();
    if(parts.Count>0)return new NameTranslation{Chinese=output.ToString(),Source=NameSource.MixedExisting,Language=country.Language,LowConfidence=parts.Any(p=>p.LowConfidence),Note="existing Chinese with foreign fragments converted; existing text is not asserted official"};
   }
   string particle;if(kind==NameKind.Surname&&Particles.TryGetValue(original,out particle))return new NameTranslation{Chinese=particle,Source=NameSource.Automatic,Language=country.Language,Note="surname particle rule"};
   var phonetic=phonetics.Convert(original,country.Language);
   if(phonetic==null||!IsChineseName(phonetic.Chinese))return null;
   return new NameTranslation{Chinese=phonetic.Chinese,Source=NameSource.Automatic,Language=phonetic.Language,LowConfidence=phonetic.LowConfidence||uncertain||country.Ordinal<0||country.LanguageApproximate,Note="automatic phonetic rule; not an official name"};
  }
  public NameTranslation TranslateDisplay(string first,string last,string common,int countryId,int personId){
   var exactResult=ExactContext(first,last,common,countryId,personId);if(exactResult!=null&&IsChineseName(exactResult.Chinese))return exactResult;
   if(!string.IsNullOrWhiteSpace(common)){
    if(Normalize(common,false)==Normalize((first??"")+" "+(last??""),false))return Compose(first,last,countryId);
    return TranslatePart(NameKind.Common,common,countryId);
   }
   return Compose(first,last,countryId);
  }
  public NameSource ClassifyExisting(string first,string last,string common,int countryId,int personId,string chinese){
   var context=ExactContext(first,last,common,countryId,personId);if(context!=null&&context.Chinese==chinese)return context.Source;
   var values=new[]{first,last,common};if(values.Count(v=>!string.IsNullOrWhiteSpace(v))==1){
    NameKind kind=!string.IsNullOrWhiteSpace(first)?NameKind.First:!string.IsNullOrWhiteSpace(last)?NameKind.Surname:NameKind.Common;
    bool uncertain;string note;string word=OfficialWord(kind,values.First(v=>!string.IsNullOrWhiteSpace(v)),Country(countryId),out note,out uncertain);
    if(word==chinese)return NameSource.OfficialComponents;
   }
   return NameSource.PreservedChinese;
  }
  NameTranslation Compose(string first,string last,int countryId){
   var a=TranslatePart(NameKind.First,first,countryId);var b=TranslatePart(NameKind.Surname,last,countryId);
   if(!string.IsNullOrWhiteSpace(first)&&a==null||!string.IsNullOrWhiteSpace(last)&&b==null)return null;
   var parts=new[]{a,b}.Where(p=>p!=null).ToList();if(parts.Count==0)return null;
   return Combine(string.Join("·",parts.Select(p=>p.Chinese)),parts,Country(countryId).Language,"combined first and surname; not an official complete-person name");
  }
  static NameTranslation Combine(string text,IList<NameTranslation> parts,string language,string note){
   bool official=parts.Any(p=>p.Source==NameSource.OfficialComponents||p.Source==NameSource.OfficialFull),existing=parts.Any(p=>p.Source==NameSource.PreservedChinese||p.Source==NameSource.MixedExisting),automatic=parts.Any(p=>p.Source==NameSource.Automatic||p.Source==NameSource.Mixed||p.Source==NameSource.MixedExisting);
   return new NameTranslation{Chinese=text,Language=language,Source=automatic?(existing?NameSource.MixedExisting:official?NameSource.Mixed:NameSource.Automatic):existing?NameSource.PreservedChinese:NameSource.OfficialComponents,LowConfidence=parts.Any(p=>p.LowConfidence),Note=note};
  }
  static List<string> SplitName(string text){
   var pieces=new List<string>();var b=new StringBuilder();
   Action flush=()=>{if(b.Length>0){pieces.Add(b.ToString());b.Clear();}};
   foreach(char c in text){if(char.IsWhiteSpace(c)){flush();}else if(c=='-'||c=='·'||c=='–'){flush();pieces.Add(c=='·'?"·":"-");}else b.Append(c);}flush();
   return pieces;
  }
  public static string SourceLabel(NameSource source){switch(source){case NameSource.OfficialFull:return "官方完整译名";case NameSource.OfficialComponents:return "官方姓名词条组合";case NameSource.Automatic:return "自动音译";case NameSource.Mixed:return "官方词条与自动音译混合";case NameSource.MixedExisting:return "原有中文与自动音译混合";default:return "原有中文保留";}}
 }
}
