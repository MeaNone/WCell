using System;
using System.IO;
using NLog;
using WCell.MPQTool;
using WCell.Tools.Maps.Constants;
using WCell.Tools.Maps.Parsing.ADT.Components;
using WCell.Tools.Maps.Structures;
using WCell.Tools.Ralek;
using WCell.Util.Graphics;

namespace WCell.Tools.Maps.Parsing.ADT
{
    public static class ADTParser
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        
        private const string baseDir = "WORLD\\MAPS\\";
        public const string Extension = ".adt";

        public static ADT Process(MpqManager mpqManager, DBCMapEntry mapId, int tileX, int tileY)
        {
            var fileName = string.Format("{0}\\{0}_{1}_{2}{3}", mapId.MapDirName, tileY, tileX, Extension);
            var filePath = Path.Combine(baseDir, fileName);
            var adt = new ADT(fileName);

            if (!mpqManager.FileExists(filePath))
            {
                log.Error("ADT file does not exist: ", filePath);
            }
            var fileReader = new BinaryReader(mpqManager.OpenFile(filePath));

            ReadMVER(fileReader, adt);

            ReadMHDR(fileReader, adt);

            if (adt.Header.offsInfo != 0)
            {
                fileReader.BaseStream.Position = adt.Header.Base + adt.Header.offsInfo;
                ReadMCIN(fileReader, adt);
            }
            if (adt.Header.offsTex != 0)
            {
                fileReader.BaseStream.Position = adt.Header.Base + adt.Header.offsTex;
                ReadMTEX(fileReader, adt);
            }
            if (adt.Header.offsModels != 0)
            {
                fileReader.BaseStream.Position = adt.Header.Base + adt.Header.offsModels;
                ReadMMDX(fileReader, adt);
            }
            if (adt.Header.offsModelIds != 0)
            {
                fileReader.BaseStream.Position = adt.Header.Base + adt.Header.offsModelIds;
                ReadMMID(fileReader, adt);
            }
            if (adt.Header.offsMapObjects != 0)
            {
                fileReader.BaseStream.Position = adt.Header.Base + adt.Header.offsMapObjects;
                ReadMWMO(fileReader, adt);
            }
            if (adt.Header.offsMapObjectIds != 0)
            {
                fileReader.BaseStream.Position = adt.Header.Base + adt.Header.offsMapObjectIds;
                ReadMWID(fileReader, adt);
            }
            if (adt.Header.offsDoodadDefinitions != 0)
            {
                fileReader.BaseStream.Position = adt.Header.Base + adt.Header.offsDoodadDefinitions;
                ReadMDDF(fileReader, adt);
            }
            if (adt.Header.offsObjectDefinitions != 0)
            {
                fileReader.BaseStream.Position = adt.Header.Base + adt.Header.offsObjectDefinitions;
                ReadMODF(fileReader, adt);
            }
            if (adt.Header.offsFlightBoundary != 0)
            {
                fileReader.BaseStream.Position = adt.Header.Base + adt.Header.offsFlightBoundary;
                ReadMFBO(fileReader, adt);
            }
            if (adt.Header.offsMH2O != 0)
            {
                fileReader.BaseStream.Position = adt.Header.Base + adt.Header.offsMH2O;
                ReadMH2O(fileReader, adt);
            }

            ReadMCNK(fileReader, adt);

            fileReader.Close();

            return adt;
        }

        static void ReadMVER(BinaryReader fileReader, ADT adt)
        {
            var type = fileReader.ReadUInt32();
            var size = fileReader.ReadUInt32();

            adt.Version = fileReader.ReadInt32();
        }

        static void ReadMHDR(BinaryReader fileReader, ADT adt)
        {
            var type = fileReader.ReadUInt32();
            var size = fileReader.ReadUInt32();

            adt.Header.Base = (uint) fileReader.BaseStream.Position;

            var pad = fileReader.ReadUInt32();

            adt.Header.offsInfo = fileReader.ReadUInt32();
            adt.Header.offsTex = fileReader.ReadUInt32();
            adt.Header.offsModels = fileReader.ReadUInt32();
            adt.Header.offsModelIds = fileReader.ReadUInt32();
            adt.Header.offsMapObjects = fileReader.ReadUInt32();
            adt.Header.offsMapObjectIds = fileReader.ReadUInt32();
            adt.Header.offsDoodadDefinitions = fileReader.ReadUInt32();
            adt.Header.offsObjectDefinitions = fileReader.ReadUInt32();
            adt.Header.offsFlightBoundary = fileReader.ReadUInt32();
            adt.Header.offsMH2O = fileReader.ReadUInt32();

            var pad3 = fileReader.ReadUInt32();
            var pad4 = fileReader.ReadUInt32();
            var pad5 = fileReader.ReadUInt32();
            var pad6 = fileReader.ReadUInt32();
            var pad7 = fileReader.ReadUInt32();
        }

        static void ReadMCIN(BinaryReader fileReader, ADT adt)
        {
            var type = fileReader.ReadUInt32();
            var size = fileReader.ReadUInt32();

            adt.MapChunkInfo = new MapChunkInfo[256];
            for (var i = 0; i < 256; i++)
            {
                var mcin = new MapChunkInfo
                               {
                                   Offset = fileReader.ReadUInt32(),
                                   Size = fileReader.ReadUInt32(),
                                   Flags = fileReader.ReadUInt32(),
                                   AsyncId = fileReader.ReadUInt32()
                               };

                if (mcin.Flags != 0)
                {
                    Console.WriteLine();
                }
                
                adt.MapChunkInfo[i] = mcin;
            }
        }

        static void ReadMTEX(BinaryReader br, ADT adt)
        {
            
        }

        static void ReadMMDX(BinaryReader fileReader, ADT adt)
        {
            var type = fileReader.ReadUInt32();
            var size = fileReader.ReadUInt32();

            var endPos = fileReader.BaseStream.Position + size;
            while (fileReader.BaseStream.Position < endPos)
            {
                if (fileReader.PeekByte() == 0)
                {
                    fileReader.BaseStream.Position++;
                }
                else
                {
                    adt.ModelFiles.Add(fileReader.ReadCString());
                }
            }
        }

        static void ReadMMID(BinaryReader fileReader, ADT adt)
        {
            var type = fileReader.ReadUInt32();
            var size = fileReader.ReadUInt32();

            var count = size/4;
            for (var i=0;i<count;i++)
            {
                adt.ModelNameOffsets.Add(fileReader.ReadInt32());
            }
        }

        static void ReadMWMO(BinaryReader fileReader, ADT adt)
        {
            var type = fileReader.ReadUInt32();
            var size = fileReader.ReadUInt32();

            var endPos = fileReader.BaseStream.Position + size;
            while (fileReader.BaseStream.Position < endPos)
            {
                if (fileReader.PeekByte() == 0)
                {
                    fileReader.BaseStream.Position++;
                }
                else
                {
                    adt.ObjectFiles.Add(fileReader.ReadCString());
                }
            }
        }

        static void ReadMWID(BinaryReader fileReader, ADT adt)
        {
            var type = fileReader.ReadUInt32();
            var size = fileReader.ReadUInt32();

            uint count = size / 4;
            for (int i = 0; i < count; i++)
            {
                adt.ObjectFileOffsets.Add(fileReader.ReadInt32());
            }
        }

        static void ReadMDDF(BinaryReader fileReader, ADT adt)
        {
            var type = fileReader.ReadUInt32();
            var size = fileReader.ReadUInt32();

            long endPos = fileReader.BaseStream.Position + size;
            while (fileReader.BaseStream.Position < endPos)
            {
                var doodadDefinition = new MapDoodadDefinition();
                var nameIndex = fileReader.ReadInt32();
                doodadDefinition.FilePath = adt.ModelFiles[nameIndex]; // 4 bytes
                doodadDefinition.UniqueId = fileReader.ReadUInt32(); // 4 bytes
                var Y = fileReader.ReadSingle();
                var Z = fileReader.ReadSingle();
                var X = fileReader.ReadSingle();
                doodadDefinition.Position = new Vector3(X, Y, Z); // 12 bytes
                doodadDefinition.OrientationA = fileReader.ReadSingle(); // 4 Bytes
                doodadDefinition.OrientationB = fileReader.ReadSingle(); // 4 Bytes
                doodadDefinition.OrientationC = fileReader.ReadSingle(); // 4 Bytes
                doodadDefinition.Scale = fileReader.ReadUInt16() / 1024f; // 2 bytes
                doodadDefinition.Flags = fileReader.ReadUInt16(); // 2 bytes
                adt.DoodadDefinitions.Add(doodadDefinition);
            }
        }

        static void ReadMODF(BinaryReader fileReader, ADT adt)
        {
            var type = fileReader.ReadUInt32();
            var size = fileReader.ReadUInt32();

            var endPos = fileReader.BaseStream.Position + size;
            while (fileReader.BaseStream.Position < endPos)
            {
                var objectDef = new MapObjectDefinition();
                var nameIndex = fileReader.ReadInt32(); // 4 bytes
                objectDef.FilePath = adt.ObjectFiles[nameIndex];
                objectDef.UniqueId = fileReader.ReadUInt32(); // 4 bytes
                // This Position appears to be in the wrong order.
                // To get WoW coords, read it as: {Y, Z, X}
                var Y = fileReader.ReadSingle();
                var Z = fileReader.ReadSingle();
                var X = fileReader.ReadSingle();
                objectDef.Position = new Vector3(X, Y, Z); // 12 bytes
                objectDef.OrientationA = fileReader.ReadSingle(); // 4 Bytes
                objectDef.OrientationB = fileReader.ReadSingle(); // 4 Bytes
                objectDef.OrientationC = fileReader.ReadSingle(); // 4 Bytes

                var min = new Vector3 {
                                          Y = fileReader.ReadSingle(),
                                          Z = fileReader.ReadSingle(),
                                          X = fileReader.ReadSingle()
                                      };

                var max = new Vector3 {
                                          Y = fileReader.ReadSingle(),
                                          Z = fileReader.ReadSingle(),
                                          X = fileReader.ReadSingle()
                                      };
                objectDef.Extents = new BoundingBox(min, max); // 12*2 bytes
                objectDef.Flags = fileReader.ReadUInt16(); // 2 bytes
                objectDef.DoodadSetId = fileReader.ReadUInt16(); // 2 bytes
                objectDef.NameSet = fileReader.ReadUInt16(); // 2 bytes
                fileReader.ReadUInt16(); // padding

                adt.ObjectDefinitions.Add(objectDef);
            }
        }

        static void ReadMFBO(BinaryReader fileReader, ADT adt)
        {
            var type = fileReader.ReadUInt32();
            var size = fileReader.ReadUInt32();
        }

        static void ReadMCNK(BinaryReader br, ADT adt)
        {
            for (var i = 0; i < 256; i++)
            {
                var chunk = ADTChunkParser.Process(br, adt.MapChunkInfo[i].Offset, adt);
                adt.MapChunks[chunk.Header.IndexY, chunk.Header.IndexX] = chunk;
            }
        }

        private struct MH20Header
        {
            /// <summary>
            /// offset to the MH2O_Information structs for this chunk
            /// </summary>
            public uint ofsInfo;
            /// <summary>
            /// The number of liquid layers present in the chunk
            /// If 0, the chunk has no liquid info
            /// If >= 1, the offsets will point to arrays
            /// </summary>
            public uint LayerCount;
            /// <summary>
            /// Offset to the 64-bit render mask
            /// </summary>
            public uint ofsRenderMask;
        }

        static void ReadMH2O(BinaryReader fileReader, ADT adt)
        {
            var sig = fileReader.ReadUInt32();
            var size = fileReader.ReadUInt32();

            var ofsMH2O = fileReader.BaseStream.Position;
            var mh20Header = new MH20Header[256];

            for (var i = 0; i < 256; i++)
            {
                mh20Header[i].ofsInfo = fileReader.ReadUInt32();
                mh20Header[i].LayerCount = fileReader.ReadUInt32();
                //if (mh20Header[i].LayerCount > 0)
                //{
                //    Console.WriteLine();
                //}
                mh20Header[i].ofsRenderMask = fileReader.ReadUInt32();
            }

            // Rows
            for (var x = 0; x < 16; x++)
            {
                // Columns
                for (var y = 0; y < 16; y++)
                {
                    // Indexing is [col, row]
                    adt.LiquidInfo[y, x] = ProcessMH2O(fileReader, mh20Header[x*16 + y], ofsMH2O);
                }
            }
        }

        private static MH2O ProcessMH2O(BinaryReader fileReader, MH20Header header, long waterSegmentBase)
        {
            var water = new MH2O();

            if (header.LayerCount == 0)
            {
                water.Header.Used = false;
                return water;
            }

            water.Header.Used = true;

            fileReader.BaseStream.Position = waterSegmentBase + header.ofsInfo;

            water.Header.Type = (FluidType)fileReader.ReadUInt16();
            water.Header.Flags = (MH2OFlags)fileReader.ReadUInt16();
            water.Header.HeightLevel1 = fileReader.ReadSingle();
            water.Header.HeightLevel2 = fileReader.ReadSingle();
            water.Header.YOffset = fileReader.ReadByte();
            water.Header.XOffset = fileReader.ReadByte();
            water.Header.Width = fileReader.ReadByte();
            water.Header.Height = fileReader.ReadByte();

            var ofsWaterFlags = fileReader.ReadUInt32();
            var ofsWaterHeightMap = fileReader.ReadUInt32();

            water.RenderBitMap = new byte[water.Header.Height];
            if (ofsWaterFlags != 0)
            {
                fileReader.BaseStream.Position = waterSegmentBase + ofsWaterFlags;
                for (var i = 0; i < water.Header.Height; i++)
                {
                    if (i < (ofsWaterHeightMap - ofsWaterFlags))
                    {
                        water.RenderBitMap[i] = fileReader.ReadByte();
                    }
                    else
                    {
                        water.RenderBitMap[i] = 0;
                    }
                }
            }


            var heightMapLen = (water.Header.Width + 1) * (water.Header.Height + 1);
            water.Heights = new float[heightMapLen];

            // If flags is 2, the chunk is for an ocean, and there is no heightmap
            if (ofsWaterHeightMap != 0 && (water.Header.Flags & MH2OFlags.Ocean) == 0)
            {
                fileReader.BaseStream.Position = waterSegmentBase + ofsWaterHeightMap;
                for (var i = 0; i < heightMapLen; i++)
                {
                    water.Heights[i] = fileReader.ReadSingle();
                    if (water.Heights[i] == 0)
                    {
                        water.Heights[i] = water.Header.HeightLevel1;
                    }
                }
            }
            else
            {
                for (var i = 0; i < heightMapLen; i++)
                {
                    water.Heights[i] = water.Header.HeightLevel1;
                }
            }

            return water;
        }
    }
}