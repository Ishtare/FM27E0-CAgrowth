// Modified build v1.1.26. FM27 archive layout credited in third-party/NOTICE.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Fm24Editor
{
    // Read named sections of a save or an archive embedded in the installed database.
    sealed class FmDataArchive : IDisposable
    {
        sealed class Section { public long At, Compressed, Plain; }
        readonly Dictionary<string,Section> sections=new Dictionary<string,Section>();
        readonly FileStream file;
        readonly CancellationToken cancel;
        static readonly Encoding Utf8=new UTF8Encoding(false,true);
        const int DirectoryLimit=64*1024*1024;
        internal FmDataArchive(string path,CancellationToken cancel,long archiveStart,params string[] required)
        {
            this.cancel=cancel;
            file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            try
            {
                using(var reader=new BinaryReader(file,Encoding.UTF8,true))
                {
                    file.Position=archiveStart;
                    byte[] header=reader.ReadBytes(26);
                    if(header.Length!=26 || header[0]!=2 || header[1]!=1 ||
                       Encoding.ASCII.GetString(header,2,4)!="fmf.") throw Invalid();
                    long payloadStart=archiveStart+26;
                    long relativeTrailer=BitConverter.ToInt64(header,9);
                    if(relativeTrailer<17 || relativeTrailer>file.Length-archiveStart-19) throw Invalid();
                    long trailer=archiveStart+9+relativeTrailer;
                    if(file.Length-trailer>DirectoryLimit) throw Invalid();
                    file.Position=trailer+9;
                    byte[] directory=Inflate(file.Length-file.Position, -1, DirectoryLimit);
                    int cursor=0;
                    ReadString(directory,ref cursor,4096); cursor+=4;
                    while(cursor<=directory.Length-12)
                    {
                        int nameLength=BitConverter.ToInt32(directory,cursor);
                        if(nameLength<1 || nameLength>256 || cursor>directory.Length-nameLength-12) break;
                        if(BitConverter.ToInt32(directory,cursor+4+nameLength)!=4) break; // end of top-level entries
                        string name=ReadString(directory,ref cursor,256);
                        string ext=ReadString(directory,ref cursor,4);
                        if(cursor>directory.Length-40) throw Invalid();
                        long relativeStart=BitConverter.ToInt64(directory,cursor);
                        long stored=BitConverter.ToInt64(directory,cursor+8), plain=BitConverter.ToInt64(directory,cursor+16);
                        cursor+=40;
                        if(ext!=".dat" || Array.IndexOf(required,name)<0) continue;
                        if(relativeStart<0 || stored<=0 || plain<=0 || relativeStart>trailer-payloadStart-stored ||
                           sections.ContainsKey(name)) throw Invalid();
                        sections.Add(name,new Section{At=payloadStart+relativeStart,Compressed=stored,Plain=plain});
                    }
                }
                if(sections.Count!=required.Length) throw Invalid();
            }
            catch { file.Dispose(); throw; }
        }
        internal static InvalidDataException Invalid()
        { return new InvalidDataException("无法核实选定来源的文件结构或数据长度。"); }
        internal static string ReadString(byte[] bytes,ref int cursor,int limit)
        {
            if(cursor<0 || cursor>bytes.Length-4) throw Invalid();
            int length=BitConverter.ToInt32(bytes,cursor); cursor+=4;
            if(length<0 || length>limit || length>bytes.Length-cursor) throw Invalid();
            string value=Utf8.GetString(bytes,cursor,length); cursor+=length;
            return value;
        }
        internal byte[] Read(string name,int limit)
        {
            var section=sections[name];
            if(section.Plain>limit) throw new InvalidDataException("所选存档的数据段超出本版本单次读取容量。");
            file.Position=section.At;
            return Inflate(section.Compressed,(int)section.Plain,limit);
        }
        byte[] Inflate(long compressed,int plain,int limit)
        {
            cancel.ThrowIfCancellationRequested();
            var output=plain<0?new MemoryStream():new MemoryStream(new byte[plain],0,plain,true,true);
            using(output)
            {
                NativeZstd.Decode(file,compressed,plain<0?limit:plain,cancel,(buffer,count)=>output.Write(buffer,0,count));
                if(plain>=0 && output.Position!=plain) throw Invalid();
                return plain<0?output.ToArray():output.GetBuffer();
            }
        }
        public void Dispose() { file.Dispose(); }
    }

    // Use the bundled x64 decoder. Restrict native library resolution to the assembly directory.
    static class NativeZstd
    {
        const string Library=@"third-party\libzstd.dll";
        [StructLayout(LayoutKind.Sequential)]
        struct Buffer { public IntPtr Data; public UIntPtr Size, Position; }
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)] static extern IntPtr ZSTD_createDStream();
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)] static extern UIntPtr ZSTD_initDStream(IntPtr stream);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)] static extern UIntPtr ZSTD_freeDStream(IntPtr stream);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)] static extern UIntPtr ZSTD_decompressStream(IntPtr stream,ref Buffer output,ref Buffer input);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)] static extern uint ZSTD_isError(UIntPtr code);
        internal const int ChunkSize=131072;
        internal static void Decode(Stream file,long compressed,int limit,CancellationToken cancel,Action<byte[],int> write)
        {
            IntPtr stream=ZSTD_createDStream();
            if(stream==IntPtr.Zero) throw new OutOfMemoryException();
            GCHandle sourcePin=default(GCHandle), targetPin=default(GCHandle);
            try
            {
                if(ZSTD_isError(ZSTD_initDStream(stream))!=0) throw FmDataArchive.Invalid();
                var source=new byte[ChunkSize]; var target=new byte[ChunkSize];
                sourcePin=GCHandle.Alloc(source,GCHandleType.Pinned); targetPin=GCHandle.Alloc(target,GCHandleType.Pinned);
                var input=new Buffer{Data=sourcePin.AddrOfPinnedObject()};
                var output=new Buffer{Data=targetPin.AddrOfPinnedObject(),Size=(UIntPtr)target.Length};
                UIntPtr remaining=(UIntPtr)1;
                int decoded=0;
                while(compressed>0 || input.Position!=input.Size || remaining!=UIntPtr.Zero)
                {
                    cancel.ThrowIfCancellationRequested();
                    if(input.Position==input.Size && compressed>0)
                    {
                        int count=file.Read(source,0,(int)Math.Min(compressed,source.Length));
                        if(count==0) throw FmDataArchive.Invalid();
                        input.Position=UIntPtr.Zero; input.Size=(UIntPtr)count; compressed-=count;
                    }
                    var previous=input.Position; output.Position=UIntPtr.Zero;
                    remaining=ZSTD_decompressStream(stream,ref output,ref input);
                    int written=(int)output.Position.ToUInt32();
                    if(ZSTD_isError(remaining)!=0 || written>limit-decoded ||
                       (previous==input.Position && written==0 && remaining!=UIntPtr.Zero)) throw FmDataArchive.Invalid();
                    if(written==0) continue;
                    decoded+=written;
                    write(target,written);
                }
            }
            finally
            {
                if(sourcePin.IsAllocated) sourcePin.Free();
                if(targetPin.IsAllocated) targetPin.Free();
                ZSTD_freeDStream(stream);
            }
        }
    }
}
