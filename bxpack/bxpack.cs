using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Be.IO;

namespace bxpack
{
    class bxpack
    {
        public static void print_help()
        {
            Console.WriteLine("BXPack by Xayrga");
            Console.WriteLine("bxpack\t<command>\t....");
            Console.WriteLine("\tunpack\t\t<in file> <out folder>");
            Console.WriteLine("\tpack\t\t<in folder> <out file>");
            return; 
        }
        static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                print_help();
                return -1;
            }
            var ret = -1; 
            if (args[0].ToLower()=="unpack")
            {
                ret = do_unpack(args[1], args[2]);
            }

            if (args[0].ToLower() == "pack")
            {
                ret = do_pack(args[1], args[2]);
            }
            return ret;
        }


        public static int[] readInt32Array(BeBinaryReader binStream, int count)
        {
            var b = new int[count];
            for (int i = 0; i < count; i++)
            {
                b[i] = binStream.ReadInt32();
            }
            return b;
        }

        static int do_pack(string infolder, string outfile)
        {
            if (!Directory.Exists(infolder))
            {
                Console.WriteLine("Error: Folder {0} didn't exist -- nothing to pack.",infolder);
                return -1; 
            }
            Stream fileHandle;
            try
            {
                fileHandle = File.OpenWrite(outfile);
            }
            catch (Exception E) { Console.WriteLine("Cannot open {0} for writing\n{1}", outfile, E.Message); return -1; }
            var fileWriter = new BeBinaryWriter(fileHandle);

            // LET US BEGIN. 
            var wsFiles = Directory.GetFiles(infolder, "*.ws");
            var ibnkFiles = Directory.GetFiles(infolder, "*.bnk");

            fileWriter.Write((int)0x10); // The WS Data will always begin at 0x10;
            fileWriter.Write(wsFiles.Length); // Write the count of the wsys
            fileWriter.Write(0x10 + wsFiles.Length * 8);
            // Explanation:
            // Each record is 8 bytes long, and the pointers start at 0x10, so our first ibnk pointer will be at the count
            // of our WS pointers times 8 bytes, plus 0x10; 
            fileWriter.Write(ibnkFiles.Length); // write count of ibnk files 

            // We have to prepare our buffer for the writes, our current position is 0x10;
            for (int i=0; i < wsFiles.Length; i++)
            {
                fileWriter.Write(0); // Just write null int for now. 
                fileWriter.Write(0); // Same for length
            }

            for (int i = 0; i < ibnkFiles.Length; i++)
            {
                fileWriter.Write(0); // Just write null int for now. 
                fileWriter.Write(0); // Same for length
            }

            fileWriter.Write(0l); // Write two longs because that's how nintendo did it. 
            fileWriter.Write(0l); // Write two longs because that's how nintendo did it. 

            fileWriter.Flush(); // flush the stream and update our shit.  

            int total_offset = (int)fileWriter.BaseStream.Position;

            fileWriter.BaseStream.Position = 0x10 + wsFiles.Length * 8; // For some reason, they're doing the ibnk files first? So we're starting there. 
            var anchor = fileWriter.BaseStream.Position;
            //Console.WriteLine("{0:X}", anchor);
            //Console.ReadLine();
            for (int i = 0; i < ibnkFiles.Length; i++)
            {
                var fileName = ibnkFiles[i];
                var wv = File.ReadAllBytes(fileName);
                //var wvh = File.OpenRead(fileName);
                //var wv = ReadToEnd(wvh);
                Console.WriteLine("FILE {0} LENGTH {1:X}", fileName, wv.Length);
                //Console.ReadLine();
                
                if (wv.Length == 4) // shitty hacks
                {
                    fileWriter.Write(0);
                    fileWriter.Write(0);
                    continue;
                }
                fileWriter.Write(total_offset);
                fileWriter.Write(wv.Length);
                anchor = fileWriter.BaseStream.Position;
                fileWriter.BaseStream.Position = total_offset;
                fileWriter.Write(wv);
                total_offset += wv.Length;
                fileWriter.BaseStream.Position = anchor;
                fileWriter.Flush();
            }



            fileWriter.BaseStream.Position = 0x10; // For some reason, they're doing the ibnk files first? So we're starting there. 
            anchor = fileWriter.BaseStream.Position;
            //Console.WriteLine("{0:X}", anchor);
            //Console.ReadLine();
            for (int i = 0; i < wsFiles.Length; i++)
            {
                var fileName = wsFiles[i];
                var wv = File.ReadAllBytes(fileName);
                //var wvh = File.OpenRead(fileName);
                //var wv = ReadToEnd(wvh);
                Console.WriteLine("FILE {0} LENGTH {1:X}", fileName, wv.Length);
                //Console.ReadLine();

                if (wv.Length == 4) // shitty hacks
                {
                    fileWriter.Write(0);
                    fileWriter.Write(0);
                    continue;
                }
                fileWriter.Write(total_offset);
                fileWriter.Write(wv.Length);
                anchor = fileWriter.BaseStream.Position;
                fileWriter.BaseStream.Position = total_offset;
                fileWriter.Write(wv);
                total_offset += wv.Length;
                fileWriter.BaseStream.Position = anchor;
                fileWriter.Flush();
            }

            fileWriter.Flush();
            return 0;
        }



        static int do_unpack(string infile, string outfolder)
        {
            if (!File.Exists(infile))
            {
                Console.WriteLine("Error: File {0} didn't exist.", infile);
                return -1; 
            }

            if (!Directory.Exists(outfolder))
            {
                try
                {
                    Directory.CreateDirectory(outfolder);
                } catch (Exception E) { Console.WriteLine("Cannot create directoy {0} because {1}", outfolder, E.Message); return -1; }
            }

            Stream fileHandle;

            try
            {
                fileHandle = File.OpenRead(infile);
            }  catch (Exception E) { Console.WriteLine("Cannot open {0}\n{1}", infile, E.Message); return -1; }

            long anchor = 0; 
            BeBinaryReader fileReader = new BeBinaryReader(fileHandle);

            var wsPointerOffset = fileReader.ReadInt32();
            var wsPointerCount = fileReader.ReadInt32();

            var ibnkPointerOffset = fileReader.ReadInt32();
            var ibnkPointerCount = fileReader.ReadInt32();

            fileReader.BaseStream.Position = wsPointerOffset;
            for (int i=0; i < wsPointerCount; i++)
            {
                var wsOffset = fileReader.ReadInt32();
                var wsLength = fileReader.ReadInt32();
                anchor = fileReader.BaseStream.Position;
                byte[] wsData;
                if (wsLength==0)
                {
                    wsData = new byte[4]; // because we don't want to shift the wsys index or any indexes in the file
                    // we at least need to make an empty file with this data in it.
                } else
                {
                    fileReader.BaseStream.Position = wsOffset;
                    wsData = fileReader.ReadBytes(wsLength);
                }
                fileReader.BaseStream.Position = anchor; // reset anchor position
                var filename = string.Format("{0}/{1:D8}.ws",outfolder,i);
                File.WriteAllBytes(filename, wsData);
            }

            fileReader.BaseStream.Position = ibnkPointerOffset;
            for (int i = 0; i < ibnkPointerCount; i++)
            {
                var ibnkOffset = fileReader.ReadInt32();
                var ibnkLength = fileReader.ReadInt32();
                anchor = fileReader.BaseStream.Position;
                byte[] ibnkData;
                if (ibnkLength == 0)
                {
                    ibnkData = new byte[4]; // because we don't want to shift the wsys index or any indexes in the file
                    // we at least need to make an empty file with this data in it.
                }
                else
                {
                    fileReader.BaseStream.Position = ibnkOffset;
                    ibnkData = fileReader.ReadBytes(ibnkLength);
                }
                fileReader.BaseStream.Position = anchor; // reset anchor position
                var filename = string.Format("{0}/{1:D8}.bnk", outfolder, i);
                File.WriteAllBytes(filename, ibnkData);
            }

            return 0;
        }
    }
}
