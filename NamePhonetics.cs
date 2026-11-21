// Modified build notice: FM27E0-CAgrowth v1.1.12; changes dated 2026-11-15.
// Adapted from this workspace's FMPersonChinese 0.2.0 authored GPL-3.0-or-later source.
// Source reference and data ownership: docs/name-translations.md. No native bridge is included.
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections.Generic;

namespace FMPersonChinese {
 public sealed class PhoneticEngine:IPhonetics {
  public const string EngineId="managed-rules-0.2.0";
  static readonly Dictionary<char,string> Letters=new Dictionary<char,string>{
   {'a',"诶"},{'b',"比"},{'c',"西"},{'d',"迪"},{'e',"伊"},{'f',"埃夫"},{'g',"吉"},{'h',"艾奇"},{'i',"艾"},{'j',"杰"},{'k',"凯"},{'l',"埃尔"},{'m',"埃姆"},{'n',"恩"},{'o',"欧"},{'p',"皮"},{'q',"丘"},{'r',"阿尔"},{'s',"埃斯"},{'t',"蒂"},{'u',"尤"},{'v',"维"},{'w',"达布柳"},{'x',"埃克斯"},{'y',"怀"},{'z',"泽德"}
  };
  // Chinese characters conventionally used as phonetic approximations, not asserted kanji/hanja.
  static readonly Dictionary<string,string> Syllables=new Dictionary<string,string>(StringComparer.Ordinal){
   {"", "阿埃伊奥乌厄艾埃奥欧于"},
   {"p","帕佩皮波普珀派佩保波皮"},{"b","巴贝比博布伯拜贝鲍博比"},
   {"t","塔特蒂托图特泰泰陶托蒂"},{"d","达德迪多杜德戴戴道多迪"},
   {"k","卡克基科库克凯凯考科基"},{"g","加格吉戈古格盖盖高戈居"},
   {"m","马梅米莫穆默迈梅毛莫米"},{"n","纳内尼诺努内奈内瑙诺尼"},
   {"f","法费菲福富弗费费福福菲"},{"v","瓦韦维沃武维瓦韦沃沃维"},
   {"s","萨塞西索苏瑟赛塞绍索叙"},{"z","扎泽齐佐祖泽扎泽造佐济"},
   {"sh","沙谢希肖舒舍沙谢肖肖叙"},{"zh","扎热日若朱热扎热若若朱"},
   {"ch","查切奇乔丘彻柴切乔乔屈"},{"j","贾杰吉乔朱杰杰杰乔乔朱"},
   {"h","哈赫希霍胡赫海黑豪霍希"},{"r","拉雷里罗鲁尔赖雷劳罗吕"},
   {"l","拉莱利洛卢勒莱雷劳洛吕"},{"y","亚耶伊约尤耶亚耶尧约于"},
   {"w","瓦韦维沃乌沃外韦沃沃维"},{"ts","察策齐措楚茨蔡策曹措齐"},
   {"ny","尼涅尼尼纽涅奈涅尼尼纽"},{"ng","昂恩英翁乌恩昂恩昂翁于"}
  };
  static readonly Dictionary<string,string> Finals=new Dictionary<string,string>(StringComparer.Ordinal){
   {"p","普"},{"b","布"},{"t","特"},{"d","德"},{"k","克"},{"g","格"},{"m","姆"},{"n","恩"},{"ng","恩"},{"f","夫"},{"v","夫"},{"s","斯"},{"z","兹"},{"sh","什"},{"zh","日"},{"ch","奇"},{"j","吉"},{"h","赫"},{"r","尔"},{"l","尔"},{"y","伊"},{"w","乌"},{"ts","茨"},{"ny","尼"}
  };
  static readonly Dictionary<string,string> NasalA=new Dictionary<string,string>{{"","昂"},{"p","潘"},{"b","班"},{"t","坦"},{"d","丹"},{"k","坎"},{"g","甘"},{"m","曼"},{"n","南"},{"f","凡"},{"v","万"},{"s","桑"},{"z","赞"},{"sh","尚"},{"zh","让"},{"ch","昌"},{"j","詹"},{"h","汉"},{"r","兰"},{"l","兰"},{"y","扬"},{"w","旺"},{"ts","灿"}};
  static readonly Dictionary<string,string> NasalE=new Dictionary<string,string>{{"","恩"},{"p","彭"},{"b","本"},{"t","滕"},{"d","登"},{"k","肯"},{"g","根"},{"m","门"},{"n","嫩"},{"f","芬"},{"v","文"},{"s","森"},{"z","曾"},{"sh","申"},{"zh","让"},{"ch","陈"},{"j","真"},{"h","亨"},{"r","伦"},{"l","伦"},{"y","延"},{"w","温"},{"ts","岑"}};
  static readonly Dictionary<string,string> NasalI=new Dictionary<string,string>{{"","因"},{"p","平"},{"b","宾"},{"t","廷"},{"d","丁"},{"k","金"},{"g","金"},{"m","明"},{"n","宁"},{"f","芬"},{"v","温"},{"s","辛"},{"z","津"},{"sh","辛"},{"zh","仁"},{"ch","钦"},{"j","金"},{"h","欣"},{"r","林"},{"l","林"},{"y","因"},{"w","温"},{"ts","钦"}};
  static readonly Dictionary<string,string> NasalO=new Dictionary<string,string>{{"","翁"},{"p","蓬"},{"b","邦"},{"t","通"},{"d","东"},{"k","孔"},{"g","贡"},{"m","蒙"},{"n","农"},{"f","丰"},{"v","翁"},{"s","松"},{"z","宗"},{"sh","雄"},{"zh","容"},{"ch","崇"},{"j","琼"},{"h","洪"},{"r","隆"},{"l","隆"},{"y","永"},{"w","翁"},{"ts","丛"}};
  static readonly Dictionary<string,string> Pinyin=BuildPinyin();
  public PhoneticEngine(){}
  public PhoneticEngine(string unusedFolder){}
  public PhoneticResult Convert(string original,string language){
   if(string.IsNullOrWhiteSpace(original)||original.Length>256||original.IndexOf('\0')>=0)return null;
   if(string.IsNullOrEmpty(language))language="en";
   string text=original.Trim().ToLowerInvariant();string inferred=InferLanguage(text,language);bool low=true;language=inferred;
   if(text.Length==1&&Letters.ContainsKey(text[0]))return new PhoneticResult{Chinese=Letters[text[0]],Language=language,Ipa="initial",LowConfidence=true};
   if(language=="cmn"){
    string han=PinyinChinese(text);if(han!=null)return new PhoneticResult{Chinese=han,Language=language,Ipa="pinyin approximation; original Han characters unknown",LowConfidence=true};
   }
   if((language=="ja"||language=="ko"||new[]{"ar","he","fa","ur","hi","bn","ta","ne","th","ka","hy","el","bo","km","lo","my"}.Contains(language))&&Regex.IsMatch(NameDictionary.Normalize(text,true),"^[a-z'’]+$")){
    string ipa=RomanIpa(text,language);string result=IpaChinese(ipa,out low);
    return new PhoneticResult{Chinese=result,Language=language,Ipa=EngineId+":"+ipa,LowConfidence=true};
   }
   string phonemes=RomanIpa(ScriptLatin(text),language);
   bool mappingLow;string chinese=IpaChinese(phonemes,out mappingLow);
   if(!NameDictionary.IsChineseName(chinese)){
    chinese=IpaChinese(RomanIpa(original,"fallback"),out mappingLow);low=true;
   }
   if(!NameDictionary.IsChineseName(chinese)){
    var output=new StringBuilder();foreach(char c in NameDictionary.Normalize(original,true)){string letter;if(Letters.TryGetValue(c,out letter))output.Append(letter);else if(char.IsDigit(c)){int number=(int)char.GetNumericValue(c);if(number>=0&&number<=9)output.Append("零一二三四五六七八九"[number]);}}
    chinese=output.ToString();low=true;
   }
   return new PhoneticResult{Chinese=chinese,Language=language,Ipa=EngineId+":"+phonemes,LowConfidence=true};
  }
  static string CanonicalConsonant(string token){
   switch(token){case "ɹ":case "ɾ":case "ʁ":case "ʀ":return "r";case "ɫ":case "ɭ":case "ɬ":return "l";case "ʃ":case "ʂ":return "sh";case "ʒ":case "ʐ":return "zh";case "tʃ":case "tɕ":case "ʈʂ":return "ch";case "dʒ":case "dʑ":case "ɖʐ":return "j";case "θ":return "s";case "ð":return "d";case "x":case "χ":case "ħ":case "ʕ":case "ç":return "h";case "ɣ":case "ɡ":case "q":return "g";case "β":return "v";case "ɸ":return "f";case "ɲ":return "ny";case "ŋ":return "ng";case "ʎ":return "y";case "j":case "ɥ":return "y";case "ɟ":return "j";case "c":return "k";default:return token;}
  }
  static string InferLanguage(string text,string language){
   if(Regex.IsMatch(text,@"[\u0400-\u04ff]"))return Regex.IsMatch(text,"[іїєґ]")?"uk":"ru";
   if(Regex.IsMatch(text,@"[\u0370-\u03ff]"))return "el";
   if(Regex.IsMatch(text,@"[\u0590-\u05ff]"))return "he";
   if(Regex.IsMatch(text,@"[\u0600-\u06ff]"))return Regex.IsMatch(text,"[پچژگ]")?"fa":"ar";
   if(Regex.IsMatch(text,@"[\u0900-\u097f]"))return "hi";
   if(Regex.IsMatch(text,@"[\u3040-\u30ff]"))return "ja";
   if(Regex.IsMatch(text,@"[\uac00-\ud7af]"))return "ko";
   if(language=="es"&&Regex.IsMatch(text,"[àèòï]|l·l"))return "ca";
   if((language=="en"||language=="en-us")&&Regex.IsMatch(text,"[øæ]"))return "da";
   if((language=="en"||language=="en-us")&&text.IndexOf('å')>=0)return "sv";
   if(language=="fr"&&Regex.IsMatch(text,"ij|(?:ink|inga|stra)$"))return "nl";
   if(language=="de"&&Regex.IsMatch(text,"[çêôû]"))return "fr";
   return language;
  }
  static string Vowel(string token){
   switch(token){case "a":case "ɑ":case "ɐ":case "æ":return "a";case "e":case "ɛ":return "e";case "i":case "ɪ":case "ɨ":case "ɯ":return "i";case "o":case "ɔ":case "ɒ":return "o";case "u":case "ʊ":return "u";case "ə":case "ɜ":case "ɝ":case "ʌ":case "ɘ":case "ɵ":return "er";case "y":case "ʏ":case "ø":case "œ":case "ɶ":return "yu";case "aɪ":return "ai";case "eɪ":return "ei";case "aʊ":return "ao";case "oʊ":case "əʊ":return "ou";case "ɔɪ":return "oi";default:return null;}
  }
  static List<string> Tokens(string ipa){
   ipa=Regex.Replace(ipa??"",@"\([^)]*\)","").Normalize(NormalizationForm.FormD);var list=new List<string>();
   string[] pairs={"tʃ","dʒ","tɕ","dʑ","ʈʂ","ɖʐ","ts","dz","aɪ","eɪ","aʊ","oʊ","əʊ","ɔɪ"};
   for(int i=0;i<ipa.Length;i++){
    char c=ipa[i];if(c=='ˈ'||c=='ˌ'||c=='ː'||c=='ˑ'||c=='ʲ'||c=='ʰ'||c=='͡'||c=='͜'||c=='ʔ')continue;
    if(char.IsWhiteSpace(c)||c=='-'||c=='·'){if(list.Count>0&&list[list.Count-1]!=" ")list.Add(" ");continue;}
    string pair=pairs.FirstOrDefault(p=>i+1<ipa.Length&&ipa.Substring(i,2)==p);if(pair!=null){list.Add(pair);i++;continue;}
    if(c=='\u0303'){if(list.Count>0)list[list.Count-1]+="~";continue;}
    if(char.GetUnicodeCategory(c)==UnicodeCategory.NonSpacingMark)continue;
    if(char.IsLetter(c))list.Add(c.ToString());
   }
   return list;
  }
  public static string IpaChinese(string ipa,out bool uncertain){
   uncertain=false;var tokens=Tokens(ipa);var output=new StringBuilder();int i=0;
   while(i<tokens.Count){if(tokens[i]==" "){i++;continue;}
    var onset=new List<string>();while(i<tokens.Count&&tokens[i]!=" "&&Vowel(tokens[i].TrimEnd('~'))==null){onset.Add(CanonicalConsonant(tokens[i]));i++;}
    if(i>=tokens.Count||tokens[i]==" "){foreach(var c in onset){string last;if(Finals.TryGetValue(c,out last))output.Append(last);else uncertain=true;}continue;}
    string vowelToken=tokens[i++],vowel=Vowel(vowelToken.TrimEnd('~'));
    for(int j=0;j<onset.Count-1;j++){string middle;if(Finals.TryGetValue(onset[j],out middle))output.Append(middle);else uncertain=true;}
    string start=onset.Count==0?"":onset[onset.Count-1];
    bool nasal=vowelToken.EndsWith("~",StringComparison.Ordinal);
    if(i<tokens.Count&&(tokens[i]=="n"||tokens[i]=="ŋ")&&(i+1==tokens.Count||tokens[i+1]==" "||Vowel(tokens[i+1].TrimEnd('~'))==null)){nasal=true;i++;}
    string syllable;
    if(nasal){var map=(vowel=="a"||vowel=="ai"||vowel=="ao")?NasalA:vowel=="o"||vowel=="ou"||vowel=="u"?NasalO:vowel=="i"||vowel=="yu"?NasalI:NasalE;if(!map.TryGetValue(start,out syllable)){syllable=map[""];uncertain=true;}}
    else{string series;if(!Syllables.TryGetValue(start,out series)){series=Syllables[""];uncertain=true;}int at=Array.IndexOf(new[]{"a","e","i","o","u","er","ai","ei","ao","ou","yu"},vowel);if(at<0){at=3;uncertain=true;}syllable=series[at].ToString();if(vowel=="oi")syllable+="伊";}
    output.Append(syllable);
   }
   return output.ToString();
  }
  static string RomanIpa(string text,string language){
   if(language=="de"||new[]{"sv","nb","da","fi","is","tr","hu"}.Contains(language))text=text.Replace("ä","ɛ").Replace("ö","ø").Replace("ü","ʏ").Replace("å","o");
   text=text.Replace("ñ","ɲ").Replace("ç",language=="tr"?"tʃ":"s").Replace("ş","ʃ").Replace("š","ʃ").Replace("ž","ʒ").Replace("č","tʃ").Replace("ć","tɕ").Replace("đ",language=="vi"?"d":"dʒ").Replace("ł","w").Replace("ń","ɲ").Replace("ß","ss").Replace("þ","θ").Replace("ð","ð").Replace("ğ","").Replace("ı","ɯ");
   text=NameDictionary.Normalize(text,true);var b=new StringBuilder();
   var replacements=new Dictionary<string,string>{{"shch","ʃtʃ"},{"sh","ʃ"},{"ch","tʃ"},{"zh","ʒ"},{"kh","x"},{"gh","ɣ"},{"th",language=="ar"?"θ":"t"},{"dh","ð"},{"ph","f"},{"ts","ts"},{"ng","ŋ"},{"ny","ɲ"},{"aa","ɑ"},{"ee","i"},{"oo","u"}};
   if(language=="en"||language=="en-us"){replacements["th"]="θ";replacements["tch"]="tʃ";replacements["dge"]="dʒ";replacements["ck"]="k";replacements["qu"]="kw";replacements["ea"]="i";replacements["ai"]="eɪ";replacements["ay"]="eɪ";replacements["oi"]="ɔɪ";replacements["oy"]="ɔɪ";replacements["oa"]="o";replacements["ow"]="aʊ";replacements["ou"]="aʊ";text=Regex.Replace(text,@"^(?:kn|gn)","n");text=Regex.Replace(text,@"^wr","r");if(text.Length>3&&!Regex.IsMatch(text,@"(?:ge|ce|le)$"))text=Regex.Replace(text,"e$","");}
   if(language=="fr"){replacements["eaux"]="o";replacements["eau"]="o";replacements["aux"]="o";replacements["au"]="o";replacements["ou"]="u";replacements["oi"]="wa";replacements["eu"]="ø";replacements["ai"]="ɛ";replacements["ei"]="ɛ";replacements["ch"]="ʃ";replacements["gn"]="ɲ";replacements["an"]="ɑ̃";replacements["on"]="ɔ̃";replacements["in"]="ɛ̃";replacements["qu"]="k";replacements["ce"]="se";replacements["ci"]="si";replacements["ge"]="ʒe";replacements["gi"]="ʒi";if(text.Length>3)text=Regex.Replace(text,@"[tdsxp]$","");if(text.Length>3)text=Regex.Replace(text,"e$","");}
   if(language=="de"){replacements["sch"]="ʃ";replacements["ch"]="x";replacements["tsch"]="tʃ";replacements["tz"]="ts";replacements["ei"]="aɪ";replacements["ai"]="aɪ";replacements["ie"]="i";replacements["eu"]="ɔɪ";replacements["au"]="aʊ";}
   if(language=="es"||language=="ca"){replacements["ll"]="j";replacements["qu"]="k";replacements["gue"]="ge";replacements["gui"]="gi";replacements["ce"]="se";replacements["ci"]="si";replacements["ny"]="ɲ";replacements["rr"]="r";}
   if(language=="pt"||language=="pt-br"){replacements["nh"]="ɲ";replacements["lh"]="ʎ";replacements["ch"]="ʃ";replacements["rr"]="h";replacements["qu"]="k";replacements["ce"]="se";replacements["ci"]="si";replacements["ge"]="ʒe";replacements["gi"]="ʒi";replacements["ou"]="o";}
   if(language=="it"){replacements["gli"]="ʎi";replacements["gn"]="ɲ";replacements["ch"]="k";replacements["gh"]="g";replacements["sce"]="ʃe";replacements["sci"]="ʃi";replacements["ce"]="tʃe";replacements["ci"]="tʃi";replacements["ge"]="dʒe";replacements["gi"]="dʒi";replacements["qu"]="kw";}
   if(language=="nl"||language=="af"){replacements["sch"]="sx";replacements["ch"]="x";replacements["ij"]="eɪ";replacements["oe"]="u";replacements["ou"]="aʊ";replacements["ui"]="ø";}
   if(language=="pl"){replacements["szcz"]="ʃtʃ";replacements["cz"]="tʃ";replacements["sz"]="ʃ";replacements["rz"]="ʒ";replacements["ch"]="x";replacements["dz"]="dz";}
   if(language=="hu"){replacements["sz"]="s";replacements["zs"]="ʒ";replacements["cs"]="tʃ";replacements["gy"]="dʒ";replacements["ny"]="ɲ";replacements["ty"]="tʃ";replacements["ly"]="j";}
   if(language=="fr"){replacements.Remove("an");replacements.Remove("on");replacements.Remove("in");text=Regex.Replace(text,@"(?:ain|ein|in|im)(?=$|[^aeiouy])","ɛ̃");text=Regex.Replace(text,@"(?:an|am)(?=$|[^aeiouy])","ɑ̃");text=Regex.Replace(text,@"(?:on|om)(?=$|[^aeiouy])","ɔ̃");}
   if(language=="sv"||language=="nb"||language=="da"){replacements["skj"]="ʃ";replacements["sj"]="ʃ";replacements["tj"]="tʃ";}
   if(language=="sq"){replacements["xh"]="dʒ";replacements["gj"]="dʒ";replacements["nj"]="ɲ";replacements["zh"]="ʒ";}
   if(language=="ja"){replacements["shi"]="ʃi";replacements["chi"]="tʃi";replacements["tsu"]="tsu";replacements["fu"]="ɸu";replacements["ji"]="dʑi";replacements["ry"]="rj";}
   if(language=="ko"){replacements["eo"]="ʌ";replacements["eu"]="ɯ";replacements["yeo"]="jʌ";replacements["ui"]="ɯi";}
   if(new[]{"hi","bn","ta","ne","th","bo","km","lo","my"}.Contains(language)){replacements["th"]="t";replacements["dh"]="d";replacements["bh"]="b";replacements["ph"]="p";replacements["kh"]="k";replacements["gh"]="g";}
   if(language=="el"){replacements["th"]="θ";replacements["ch"]="x";replacements["ph"]="f";replacements["ou"]="u";replacements["ei"]="i";replacements["ai"]="e";}
   if(language=="he"){replacements["ch"]="x";replacements["tz"]="ts";}
   var keys=replacements.Keys.OrderByDescending(k=>k.Length).ToArray();
   for(int i=0;i<text.Length;){string match=keys.FirstOrDefault(k=>i+k.Length<=text.Length&&text.Substring(i,k.Length)==k);if(match!=null){b.Append(replacements[match]);i+=match.Length;continue;}
    char c=text[i++];
    if(c=='h'&&(language=="es"||language=="it"||language=="fr"))continue;
    if(c=='y')b.Append(language=="fi"||language=="sv"?"y":"j");
    else if(c=='j')b.Append(language=="es"?"x":language=="fr"||language.StartsWith("pt",StringComparison.Ordinal)||language=="tr"?"ʒ":new[]{"de","nl","af","sv","nb","da","fi","ru","uk","bg","mk","sr","hr","cs","sk","sl","pl","hu"}.Contains(language)?"j":language=="ja"||language=="ko"?"dʑ":"dʒ");
    else if(c=='w')b.Append(language=="de"||language=="pl"?"v":"w");
    else if(c=='v')b.Append(language=="de"?"f":language=="es"?"b":"v");
    else if(c=='z')b.Append(language=="de"?"ts":language=="es"?"s":"z");
    else if(c=='s'&&language=="hu")b.Append('ʃ');
    else if(c=='g'&&(language=="nl"||language=="af"))b.Append('ɣ');
    else if(c=='u'&&language=="fr")b.Append('y');
    else if(c=='c')b.Append(language=="tr"?"dʒ":language=="id"?"tʃ":new[]{"pl","cs","sk","sl","hr","sr","sq","hu"}.Contains(language)?"ts":"k");
    else if(c=='q')b.Append('k');else if(c=='x')b.Append("ks");else if(char.IsLetter(c)||char.IsWhiteSpace(c)||c=='\u0303')b.Append(c);
   }
   return b.ToString();
  }
  static readonly Dictionary<char,string> ScriptLetters=BuildScriptLetters();
  static Dictionary<char,string> BuildScriptLetters(){
   var map=new Dictionary<char,string>();
   foreach(string row in new[]{
    "а:a|б:b|в:v|г:g|д:d|е:e|ё:jo|ж:ʒ|з:z|и:i|й:j|к:k|л:l|м:m|н:n|о:o|п:p|р:r|с:s|т:t|у:u|ф:f|х:x|ц:ts|ч:tʃ|ш:ʃ|щ:ʃtʃ|ъ:|ы:ɯ|ь:|э:e|ю:ju|я:ja|і:i|ї:ji|є:je|ґ:g|ђ:dʒ|ј:j|љ:lj|њ:ɲ|ћ:tʃ|џ:dʒ|ќ:k|ѓ:g|ө:ø|ү:y",
    "α:a|β:v|γ:g|δ:ð|ε:e|ζ:z|η:i|θ:θ|ι:i|κ:k|λ:l|μ:m|ν:n|ξ:ks|ο:o|π:p|ρ:r|σ:s|ς:s|τ:t|υ:i|φ:f|χ:x|ψ:ps|ω:o",
    "ا:a|أ:a|إ:i|آ:a|ب:b|ت:t|ث:θ|ج:dʒ|ح:h|خ:x|د:d|ذ:ð|ر:r|ز:z|س:s|ش:ʃ|ص:s|ض:d|ط:t|ظ:z|ع:a|غ:g|ف:f|ق:k|ك:k|ل:l|م:m|ن:n|ه:h|و:u|ي:i|ى:a|ة:a|ء:|ئ:i|ؤ:u|پ:p|چ:tʃ|ژ:ʒ|گ:g|ک:k|ی:i|ے:e|ں:n|ھ:h|ٹ:t|ڈ:d|ڑ:r|ڤ:v",
    "א:a|ב:b|ג:g|ד:d|ה:h|ו:u|ז:z|ח:x|ט:t|י:i|כ:k|ך:k|ל:l|מ:m|ם:m|נ:n|ן:n|ס:s|ע:a|פ:p|ף:f|צ:ts|ץ:ts|ק:k|ר:r|ש:ʃ|ת:t",
    "あ:a|い:i|う:u|え:e|お:o|か:ka|き:ki|く:ku|け:ke|こ:ko|さ:sa|し:shi|す:su|せ:se|そ:so|た:ta|ち:chi|つ:tsu|て:te|と:to|な:na|に:ni|ぬ:nu|ね:ne|の:no|は:ha|ひ:hi|ふ:fu|へ:he|ほ:ho|ま:ma|み:mi|む:mu|め:me|も:mo|や:ja|ゆ:ju|よ:jo|ら:ra|り:ri|る:ru|れ:re|ろ:ro|わ:wa|を:o|ん:n|が:ga|ぎ:gi|ぐ:gu|げ:ge|ご:go|ざ:za|じ:ji|ず:zu|ぜ:ze|ぞ:zo|だ:da|ぢ:ji|づ:zu|で:de|ど:do|ば:ba|び:bi|ぶ:bu|べ:be|ぼ:bo|ぱ:pa|ぴ:pi|ぷ:pu|ぺ:pe|ぽ:po|ゃ:ja|ゅ:ju|ょ:jo|っ:|ー:",
    "अ:a|आ:a|इ:i|ई:i|उ:u|ऊ:u|ए:e|ऐ:ai|ओ:o|औ:au|क:ka|ख:ka|ग:ga|घ:ga|ङ:ŋa|च:tʃa|छ:tʃa|ज:dʒa|झ:dʒa|ञ:ɲa|ट:ta|ठ:ta|ड:da|ढ:da|ण:na|त:ta|थ:ta|द:da|ध:da|न:na|प:pa|फ:pa|ब:ba|भ:ba|म:ma|य:ja|र:ra|ल:la|व:va|श:ʃa|ष:ʃa|स:sa|ह:ha|ा:a|ि:i|ी:i|ु:u|ू:u|े:e|ै:ai|ो:o|ौ:au|ं:n|्:",
    "ა:a|ბ:b|გ:g|დ:d|ე:e|ვ:v|ზ:z|თ:t|ი:i|კ:k|ლ:l|მ:m|ნ:n|ო:o|პ:p|ჟ:ʒ|რ:r|ს:s|ტ:t|უ:u|ფ:p|ქ:k|ღ:g|ყ:k|შ:ʃ|ჩ:tʃ|ც:ts|ძ:dz|წ:ts|ჭ:tʃ|ხ:x|ჯ:dʒ|ჰ:h"
   })foreach(string pair in row.Split('|')){int at=pair.IndexOf(':');if(at==1)map[pair[0]]=pair.Substring(2);}
   return map;
  }
  static string ScriptLatin(string text){
   var b=new StringBuilder();foreach(char original in text.Normalize(NormalizationForm.FormC)){
    char c=original;if(c>=0x30a1&&c<=0x30f6)c=(char)(c-0x60);string value;
    if(ScriptLetters.TryGetValue(c,out value)){b.Append(value);continue;}
    if(c>=0xac00&&c<=0xd7af){int n=c-0xac00;string[] lead={"k","k","n","t","t","r","m","p","p","s","s","","ch","ch","ch","k","t","p","h"};string[] vowel={"a","e","ya","ye","eo","e","yeo","ye","o","wa","we","oe","yo","u","wo","we","wi","yu","eu","ui","i"};string[] tail={"","k","k","ks","n","nch","nh","t","l","lk","lm","lp","ls","lt","lp","lh","m","p","ps","t","t","ng","t","t","k","t","p","h"};b.Append(lead[n/588]).Append(vowel[(n/28)%21]).Append(tail[n%28]);continue;}
    if((c>=0x0370&&c<=0x04ff)){string plain=NameDictionary.Normalize(c.ToString(),true);if(plain.Length==1&&ScriptLetters.TryGetValue(plain[0],out value)){b.Append(value);continue;}}
    b.Append(c);
   }return b.ToString();
  }
  static string Cyrillic(string value,string language){
   var pairs=new[]{new[]{"shch","щ"},new[]{"sch","щ"},new[]{"zh","ж"},new[]{"kh","х"},new[]{"sh","ш"},new[]{"ch","ч"},new[]{"ts","ц"},new[]{"ya","я"},new[]{"yu","ю"},new[]{"yo","ё"},new[]{"yi","ї"},new[]{"ye","е"},new[]{"iy","ий"},new[]{"ii","ий"}};
   string latin="abvgdezijklmnoprstufhcy",cyrillic="абвгдезийклмнопрстуфхцы";var b=new StringBuilder();
   for(int i=0;i<value.Length;){var p=pairs.FirstOrDefault(v=>i+v[0].Length<=value.Length&&value.Substring(i,v[0].Length)==v[0]);if(p!=null){b.Append(p[1]);i+=p[0].Length;continue;}char c=value[i++];int at=latin.IndexOf(c);if(at>=0)b.Append(cyrillic[at]);else if(c=='w')b.Append('в');else if(c=='q')b.Append('к');else if(c=='x')b.Append("кс");}
   return b.ToString();
  }
  static Dictionary<string,string> BuildPinyin(){
   var map=new Dictionary<string,string>(StringComparer.Ordinal);
   string words="a阿 ai艾 an安 ang昂 ao奥 ba巴 bai白 ban班 bang邦 bao保 bei贝 ben本 beng崩 bi比 bian边 biao彪 bie别 bin斌 bing冰 bo波 bu布 ca察 cai蔡 can灿 cang仓 cao曹 ce策 cen岑 ceng曾 cha查 chai柴 chan昌 chang常 chao超 che车 chen陈 cheng成 chi迟 chong崇 chou仇 chu楚 chuai揣 chuan川 chuang创 chui崔 chun春 chuo绰 ci慈 cong聪 cou凑 cu粗 cuan蹿 cui崔 cun存 cuo措 da达 dai戴 dan丹 dang党 dao道 de德 dei得 den登 deng邓 di迪 dia嗲 dian典 diao刁 die迭 ding丁 diu丢 dong东 dou窦 du杜 duan段 dui兑 dun敦 duo多 e厄 ei诶 en恩 eng恩 er尔 fa法 fan范 fang方 fei飞 fen芬 feng冯 fo佛 fou否 fu福 ga加 gai盖 gan甘 gang刚 gao高 ge格 gei给 gen根 geng耿 gong功 gou苟 gu顾 gua瓜 guai乖 guan关 guang光 gui桂 gun衮 guo郭 ha哈 hai海 han韩 hang杭 hao郝 he何 hei黑 hen亨 heng恒 hong宏 hou侯 hu胡 hua华 huai怀 huan欢 huang黄 hui辉 hun浑 huo霍 ji吉 jia贾 jian建 jiang江 jiao焦 jie杰 jin金 jing景 jiong炯 jiu久 ju居 juan娟 jue觉 jun俊 ka卡 kai凯 kan侃 kang康 kao考 ke科 ken肯 keng铿 kong孔 kou寇 ku库 kua夸 kuai快 kuan宽 kuang匡 kui奎 kun坤 kuo阔 la拉 lai来 lan兰 lang郎 lao劳 le乐 lei雷 leng冷 li李 lia俩 lian连 liang梁 liao廖 lie列 lin林 ling凌 liu刘 long龙 lou娄 lu卢 luan栾 lun伦 luo罗 lv吕 lve略 ma马 mai麦 man曼 mang芒 mao毛 me么 mei梅 men门 meng孟 mi米 mian绵 miao苗 mie咩 min敏 ming明 miu缪 mo莫 mou牟 mu穆 na纳 nai奈 nan南 nang囊 nao闹 ne内 nei内 nen嫩 neng能 ni尼 nian年 niang娘 niao鸟 nie聂 nin宁 ning宁 niu牛 nong农 nou耨 nu努 nuan暖 nuo诺 nv女 nve虐 o奥 ou欧 pa帕 pai派 pan潘 pang庞 pao保 pei佩 pen彭 peng彭 pi皮 pian翩 piao朴 pie撇 pin品 ping平 po波 pou剖 pu普 qi齐 qia恰 qian钱 qiang强 qiao乔 qie切 qin秦 qing清 qiong琼 qiu邱 qu曲 quan全 que阙 qun群 ran冉 rang让 rao饶 re热 ren任 reng仍 ri日 rong荣 rou柔 ru儒 ruan阮 rui瑞 run润 ruo若 sa萨 sai赛 san三 sang桑 sao绍 se瑟 sen森 seng僧 sha沙 shai晒 shan山 shang尚 shao邵 she舍 shei谁 shen沈 sheng盛 shi石 shou寿 shu舒 shua刷 shuai帅 shuan栓 shuang双 shui水 shun顺 shuo硕 si斯 song宋 sou叟 su苏 suan蒜 sui隋 sun孙 suo索 ta塔 tai泰 tan谭 tang唐 tao陶 te特 teng腾 ti提 tian田 tiao条 tie铁 ting廷 tong佟 tou头 tu涂 tuan团 tui推 tun屯 tuo托 wa瓦 wai外 wan万 wang王 wei魏 wen文 weng翁 wo沃 wu吴 xi西 xia夏 xian先 xiang向 xiao肖 xie谢 xin新 xing邢 xiong熊 xiu修 xu徐 xuan轩 xue薛 xun荀 ya亚 yan严 yang杨 yao姚 ye叶 yi伊 yin尹 ying英 yong永 you尤 yu于 yuan袁 yue岳 yun云 za扎 zai载 zan赞 zang臧 zao早 ze泽 zei贼 zen曾 zeng曾 zha查 zhai翟 zhan詹 zhang张 zhao赵 zhe哲 zhei这 zhen甄 zheng郑 zhi志 zhong钟 zhou周 zhu朱 zhua抓 zhuai拽 zhuan专 zhuang庄 zhui追 zhun准 zhuo卓 zi子 zong宗 zou邹 zu祖 zuan钻 zui最 zun尊 zuo左";
   foreach(string word in words.Split(' ')){if(word.Length>1)map[word.Substring(0,word.Length-1)]=word.Substring(word.Length-1);}
   // Given-name fallback characters are phonetic choices; surname dictionary entries win earlier.
   map["guo"]="国";map["zhang"]="章";map["li"]="立";map["yang"]="阳";map["liao"]="辽";map["chen"]="晨";
   return map;
  }
  static string PinyinChinese(string value){
   value=NameDictionary.Normalize(value.Replace("ü","v").Replace("Ü","v"),true).Replace("'","");int n=value.Length;var best=new string[n+1];best[n]="";
   for(int i=n-1;i>=0;i--){if(char.IsWhiteSpace(value[i])){best[i]=best[i+1];continue;}for(int length=Math.Min(6,n-i);length>=1;length--){string h;if(best[i+length]!=null&&Pinyin.TryGetValue(value.Substring(i,length),out h)){best[i]=h+best[i+length];break;}}}
   return best[0];
  }
 }
}
