using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SubtitlePatcher
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Smart Alex - System Interface [Subtitle Patcher]";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
  __________________________________________________________
 /                                                          \
|   _____ _____ ____  _        _      _____ _____ _____ _____|
|  |_   _| ____/ ___|| |      / \    | ____|  ___|  ___| ____|
|    | | |  _| \___ \| |     / _ \   |  _| | |_  | |_  |  _|  
|    | | | |___ ___) | |___ / ___ \  | |___|  _| |  _| | |___ 
|    |_| |_____|____/|_____/_/   \_\ |_____|_|   |_|   |_|_____|
|                                                            |
|                 SUBTITLE INTERFACE RECALIBRATOR            |
 \__________________________________________________________/
            ");

            if (args.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[SYSTEM] Smart Alex: \"Tex, I need the path to the Assembly-CSharp.dll to begin recalibration.\"");
                Console.ResetColor();
                Console.WriteLine("\nUsage: SubtitlePatcher.exe <path-to-dll>");
                return;
            }

            string assemblyPath = args[0];

            if (!File.Exists(assemblyPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] File not found: {assemblyPath}");
                Console.ResetColor();
                return;
            }

            try
            {
                // Create backup
                string backupPath = assemblyPath + ".backup";
                if (!File.Exists(backupPath))
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"[SYSTEM] Creating secure backup: {Path.GetFileName(backupPath)}");
                    File.Copy(assemblyPath, backupPath, false);
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n[SYSTEM] Accessing assembly: {Path.GetFileName(assemblyPath)}");
                Console.WriteLine("[SYSTEM] Smart Alex: \"Initializing override sequence...\"");

                // Set up assembly resolver
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath));

                var readerParams = new ReaderParameters
                {
                    ReadWrite = true,
                    AssemblyResolver = resolver
                };

                var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParams);

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("\n[PATCH] Recalibrating Settings Class...");
                PatchSettingsClass(assembly);

                Console.WriteLine("[PATCH] Intercepting VoiceoverDialogBox...");
                PatchVoiceoverDialogBox(assembly);

                Console.WriteLine("[PATCH] Overriding VideoPlayer Subtitles...");
                PatchVideoPlayer(assembly);

                Console.WriteLine("[PATCH] Integrating Settings Menu UI...");
                PatchSettingsMenu(assembly);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n[SYSTEM] Finalizing write operations...");
                assembly.Write();
                assembly.Dispose();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n[SUCCESS] Smart Alex: \"Interface recalibration complete, Tex.\"");
                Console.WriteLine("[SUCCESS] Subtitle size control has been integrated into the Video Settings menu.");
                Console.ResetColor();
                
                Console.WriteLine("\nPress any key to close the terminal...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[CRITICAL ERROR] {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                Console.ReadKey();
            }
        }

        static void InjectConfigClass(AssemblyDefinition assembly)
        {
            // Check if already injected
            if (assembly.MainModule.Types.Any(t => t.Name == "SubtitleSizeConfig"))
            {
                Console.WriteLine("  SubtitleSizeConfig class already exists, skipping injection.");
                return;
            }

            // Create the config class
            var configClass = new TypeDefinition(
                "",
                "SubtitleSizeConfig",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit,
                assembly.MainModule.TypeSystem.Object
            );

            // Add to assembly
            assembly.MainModule.Types.Add(configClass);

            // Create static field: private static int s_fontSize = 24
            var fontSizeField = new FieldDefinition(
                "s_fontSize",
                FieldAttributes.Private | FieldAttributes.Static,
                assembly.MainModule.TypeSystem.Int32
            );
            configClass.Fields.Add(fontSizeField);

            // Create static field: private static bool s_loaded = false
            var loadedField = new FieldDefinition(
                "s_loaded",
                FieldAttributes.Private | FieldAttributes.Static,
                assembly.MainModule.TypeSystem.Boolean
            );
            configClass.Fields.Add(loadedField);

            // Create GetFontSize() method
            var getFontSizeMethod = new MethodDefinition(
                "GetFontSize",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                assembly.MainModule.TypeSystem.Int32
            );
            configClass.Methods.Add(getFontSizeMethod);

            // Build the method IL
            var il = getFontSizeMethod.Body.GetILProcessor();

            // if (!s_loaded) LoadConfig();
            var loadedLabel = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Ldsfld, loadedField));
            il.Append(il.Create(OpCodes.Brtrue_S, loadedLabel));
            il.Append(il.Create(OpCodes.Call, CreateLoadConfigMethod(assembly, configClass, fontSizeField, loadedField)));
            il.Append(loadedLabel);

            // return s_fontSize;
            il.Append(il.Create(OpCodes.Ldsfld, fontSizeField));
            il.Append(il.Create(OpCodes.Ret));

            Console.WriteLine("  ✓ SubtitleSizeConfig class injected");
        }

        static MethodDefinition CreateLoadConfigMethod(AssemblyDefinition assembly, TypeDefinition configClass, FieldDefinition fontSizeField, FieldDefinition loadedField)
        {
            var method = new MethodDefinition(
                "LoadConfig",
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig,
                assembly.MainModule.TypeSystem.Void
            );
            configClass.Methods.Add(method);

            var il = method.Body.GetILProcessor();

            // Import required types and methods from mscorlib (the game's reference)
            var stringType = assembly.MainModule.TypeSystem.String;

            // Find mscorlib in the game's references
            var mscorlibRef = assembly.MainModule.AssemblyReferences.FirstOrDefault(r => r.Name == "mscorlib");
            if (mscorlibRef == null)
            {
                throw new Exception("mscorlib not found in assembly references");
            }

            // Import System.IO.File methods from mscorlib
            var fileTypeRef = new TypeReference("System.IO", "File", assembly.MainModule, mscorlibRef);
            var fileExistsMethod = new MethodReference("Exists", assembly.MainModule.TypeSystem.Boolean, fileTypeRef)
            {
                HasThis = false,
                Parameters = { new ParameterDefinition(stringType) }
            };
            var stringArrayType = new ArrayType(stringType);
            var fileReadAllLinesMethod = new MethodReference("ReadAllLines", stringArrayType, fileTypeRef)
            {
                HasThis = false,
                Parameters = { new ParameterDefinition(stringType) }
            };

            // Import string methods
            var stringTypeRef = assembly.MainModule.TypeSystem.String;
            var stringStartsWith = new MethodReference("StartsWith", assembly.MainModule.TypeSystem.Boolean, stringTypeRef)
            {
                HasThis = true,
                Parameters = { new ParameterDefinition(stringType) }
            };
            var stringSubstring = new MethodReference("Substring", stringType, stringTypeRef)
            {
                HasThis = true,
                Parameters = { new ParameterDefinition(assembly.MainModule.TypeSystem.Int32) }
            };
            var stringTrim = new MethodReference("Trim", stringType, stringTypeRef)
            {
                HasThis = true
            };

            // Import int.TryParse
            var intTypeRef = assembly.MainModule.TypeSystem.Int32;
            var int32TypeRef = new TypeReference("System", "Int32", assembly.MainModule, mscorlibRef);
            var intTryParse = new MethodReference("TryParse", assembly.MainModule.TypeSystem.Boolean, int32TypeRef)
            {
                HasThis = false,
                Parameters = {
                    new ParameterDefinition(stringType),
                    new ParameterDefinition(new ByReferenceType(intTypeRef))
                }
            };

            // string configPath = "TeslaEffect_Data/Managed/SubtitleSize.cfg"
            il.Append(il.Create(OpCodes.Ldstr, "TeslaEffect_Data/Managed/SubtitleSize.cfg"));
            var configPathVar = new VariableDefinition(stringType);
            method.Body.Variables.Add(configPathVar);
            il.Append(il.Create(OpCodes.Stloc, configPathVar));

            // if (File.Exists(configPath))
            il.Append(il.Create(OpCodes.Ldloc, configPathVar));
            il.Append(il.Create(OpCodes.Call, fileExistsMethod));
            var endIfLabel = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Brfalse, endIfLabel));

            // string[] lines = File.ReadAllLines(configPath)
            il.Append(il.Create(OpCodes.Ldloc, configPathVar));
            il.Append(il.Create(OpCodes.Call, fileReadAllLinesMethod));
            var linesVar = new VariableDefinition(stringArrayType);
            method.Body.Variables.Add(linesVar);
            il.Append(il.Create(OpCodes.Stloc, linesVar));

            // foreach loop setup
            var loopIndexVar = new VariableDefinition(assembly.MainModule.TypeSystem.Int32);
            method.Body.Variables.Add(loopIndexVar);
            il.Append(il.Create(OpCodes.Ldc_I4_0));
            il.Append(il.Create(OpCodes.Stloc, loopIndexVar));

            var loopConditionLabel = il.Create(OpCodes.Ldloc, loopIndexVar);
            var loopBodyLabel = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Br, loopConditionLabel));

            // Loop body
            il.Append(loopBodyLabel);

            // string line = lines[i]
            il.Append(il.Create(OpCodes.Ldloc, linesVar));
            il.Append(il.Create(OpCodes.Ldloc, loopIndexVar));
            il.Append(il.Create(OpCodes.Ldelem_Ref));
            var lineVar = new VariableDefinition(stringType);
            method.Body.Variables.Add(lineVar);
            il.Append(il.Create(OpCodes.Stloc, lineVar));

            // if (line.StartsWith("SubtitleFontSize="))
            il.Append(il.Create(OpCodes.Ldloc, lineVar));
            il.Append(il.Create(OpCodes.Ldstr, "SubtitleFontSize="));
            il.Append(il.Create(OpCodes.Callvirt, stringStartsWith));
            var nextIterationLabel = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Brfalse, nextIterationLabel));

            // string value = line.Substring(17).Trim()
            il.Append(il.Create(OpCodes.Ldloc, lineVar));
            il.Append(il.Create(OpCodes.Ldc_I4, 17)); // length of "SubtitleFontSize="
            il.Append(il.Create(OpCodes.Callvirt, stringSubstring));
            il.Append(il.Create(OpCodes.Callvirt, stringTrim));
            var valueVar = new VariableDefinition(stringType);
            method.Body.Variables.Add(valueVar);
            il.Append(il.Create(OpCodes.Stloc, valueVar));

            // int size; if (int.TryParse(value, out size))
            var sizeVar = new VariableDefinition(assembly.MainModule.TypeSystem.Int32);
            method.Body.Variables.Add(sizeVar);
            il.Append(il.Create(OpCodes.Ldloc, valueVar));
            il.Append(il.Create(OpCodes.Ldloca, sizeVar));
            il.Append(il.Create(OpCodes.Call, intTryParse));
            il.Append(il.Create(OpCodes.Brfalse, nextIterationLabel));

            // s_fontSize = size
            il.Append(il.Create(OpCodes.Ldloc, sizeVar));
            il.Append(il.Create(OpCodes.Stsfld, fontSizeField));

            // break
            il.Append(il.Create(OpCodes.Br, endIfLabel));

            // i++
            il.Append(nextIterationLabel);
            il.Append(il.Create(OpCodes.Ldloc, loopIndexVar));
            il.Append(il.Create(OpCodes.Ldc_I4_1));
            il.Append(il.Create(OpCodes.Add));
            il.Append(il.Create(OpCodes.Stloc, loopIndexVar));

            // Loop condition: i < lines.Length
            il.Append(loopConditionLabel);
            il.Append(il.Create(OpCodes.Ldloc, linesVar));
            il.Append(il.Create(OpCodes.Ldlen));
            il.Append(il.Create(OpCodes.Conv_I4));
            il.Append(il.Create(OpCodes.Blt, loopBodyLabel));

            // else: s_fontSize = 24 (default)
            il.Append(endIfLabel);
            var afterElseLabel = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Ldsfld, fontSizeField));
            il.Append(il.Create(OpCodes.Ldc_I4_0));
            il.Append(il.Create(OpCodes.Bne_Un, afterElseLabel));
            il.Append(il.Create(OpCodes.Ldc_I4, 24));
            il.Append(il.Create(OpCodes.Stsfld, fontSizeField));
            il.Append(afterElseLabel);

            // s_loaded = true
            il.Append(il.Create(OpCodes.Ldc_I4_1));
            il.Append(il.Create(OpCodes.Stsfld, loadedField));

            il.Append(il.Create(OpCodes.Ret));

            return method;
        }

        static void PatchVoiceoverDialogBox(AssemblyDefinition assembly)
        {
            var voiceoverClass = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "VoiceoverDialogBox");
            if (voiceoverClass == null)
            {
                Console.WriteLine("  WARNING: VoiceoverDialogBox class not found!");
                return;
            }

            var setTextMethod = voiceoverClass.Methods.FirstOrDefault(m => m.Name == "SetText");
            if (setTextMethod == null)
            {
                Console.WriteLine("  WARNING: SetText method not found!");
                return;
            }

            // Find Settings.GetSubtitleSize
            var settingsClass = assembly.MainModule.Types.First(t => t.Name == "Settings");
            var getSubtitleSizeMethod = settingsClass.Methods.First(m => m.Name == "GetSubtitleSize");
            var getSubtitleSizeRef = assembly.MainModule.ImportReference(getSubtitleSizeMethod);

            // Find the line: ldsfld SUBTITLE_FONT_SIZE followed by callvirt SetFontSize
            var il = setTextMethod.Body.GetILProcessor();
            var instructions = setTextMethod.Body.Instructions;

            for (int i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];

                // Look for: ldsfld int32 VoiceoverDialogBox::SUBTITLE_FONT_SIZE
                if (instruction.OpCode == OpCodes.Ldsfld &&
                    instruction.Operand is FieldReference field &&
                    field.Name == "SUBTITLE_FONT_SIZE")
                {
                    // Check if next instruction is calling SetFontSize
                    if (i + 1 < instructions.Count)
                    {
                        var nextInstr = instructions[i + 1];
                        if (nextInstr.OpCode == OpCodes.Callvirt &&
                            nextInstr.Operand is MethodReference method &&
                            method.Name == "SetFontSize")
                        {
                            // Replace ldsfld with call to Settings.GetSubtitleSize()
                            il.Replace(instruction, il.Create(OpCodes.Call, getSubtitleSizeRef));
                            Console.WriteLine($"  ✓ Patched at IL_{instruction.Offset:X4}");
                            break;
                        }
                    }
                }
            }
        }

        static void PatchVideoPlayer(AssemblyDefinition assembly)
        {
            var videoPlayerClass = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "VideoPlayer");
            if (videoPlayerClass == null)
            {
                Console.WriteLine("  WARNING: VideoPlayer class not found!");
                return;
            }

            var setSubtitleMethod = videoPlayerClass.Methods.FirstOrDefault(m => m.Name == "SetSubtitle");
            if (setSubtitleMethod == null)
            {
                Console.WriteLine("  WARNING: SetSubtitle method not found!");
                return;
            }

            // Find Settings.GetSubtitleSize
            var settingsClass = assembly.MainModule.Types.First(t => t.Name == "Settings");
            var getSubtitleSizeMethod = settingsClass.Methods.First(m => m.Name == "GetSubtitleSize");
            var getSubtitleSizeRef = assembly.MainModule.ImportReference(getSubtitleSizeMethod);

            // Find the line: ldsfld SUBTITLE_FONT_SIZE followed by callvirt SetFontSize
            var il = setSubtitleMethod.Body.GetILProcessor();
            var instructions = setSubtitleMethod.Body.Instructions;

            for (int i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];

                // Look for: ldsfld int32 VideoPlayer::SUBTITLE_FONT_SIZE
                if (instruction.OpCode == OpCodes.Ldsfld &&
                    instruction.Operand is FieldReference field &&
                    field.Name == "SUBTITLE_FONT_SIZE")
                {
                    // Check if next instruction is calling SetFontSize
                    if (i + 1 < instructions.Count)
                    {
                        var nextInstr = instructions[i + 1];
                        if (nextInstr.OpCode == OpCodes.Callvirt &&
                            nextInstr.Operand is MethodReference method &&
                            method.Name == "SetFontSize")
                        {
                            // Replace ldsfld with call to Settings.GetSubtitleSize()
                            il.Replace(instruction, il.Create(OpCodes.Call, getSubtitleSizeRef));
                            Console.WriteLine($"  ✓ Patched at IL_{instruction.Offset:X4}");
                            break;
                        }
                    }
                }
            }
        }

        static void PatchSettingsClass(AssemblyDefinition assembly)
        {
            var settingsClass = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "Settings");
            if (settingsClass == null)
            {
                Console.WriteLine("  WARNING: Settings class not found!");
                return;
            }

            // Add subtitle size list field (static)
            var subtitleSizeListField = new FieldDefinition(
                "m_iSubtitleSizeList",
                FieldAttributes.Private | FieldAttributes.Static,
                assembly.MainModule.ImportReference(typeof(int[]))
            );
            settingsClass.Fields.Add(subtitleSizeListField);
            Console.WriteLine("  ✓ Added m_iSubtitleSizeList field");

            // Add subtitle size field (static)
            var subtitleSizeField = new FieldDefinition(
                "m_iSubtitleSize",
                FieldAttributes.Private | FieldAttributes.Static,
                assembly.MainModule.TypeSystem.Int32
            );
            settingsClass.Fields.Add(subtitleSizeField);
            Console.WriteLine("  ✓ Added m_iSubtitleSize field");

            // Initialize the list in .cctor
            PatchSettingsConstructor(assembly, settingsClass, subtitleSizeListField, subtitleSizeField);

            // Create GetSubtitleSize method
            CreateGetSubtitleSizeMethod(assembly, settingsClass, subtitleSizeField);

            // Create SetSubtitleSize method
            CreateSetSubtitleSizeMethod(assembly, settingsClass, subtitleSizeField, subtitleSizeListField);

            // Create GetSubtitleSizeList method
            CreateGetSubtitleSizeListMethod(assembly, settingsClass, subtitleSizeListField);
        }

        static void PatchSettingsConstructor(AssemblyDefinition assembly, TypeDefinition settingsClass, FieldDefinition listField, FieldDefinition sizeField)
        {
            var cctor = settingsClass.Methods.FirstOrDefault(m => m.Name == ".cctor");
            if (cctor == null)
            {
                // Create one if it doesn't exist
                cctor = new MethodDefinition(
                    ".cctor",
                    MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                    assembly.MainModule.TypeSystem.Void
                );
                settingsClass.Methods.Add(cctor);
                var il = cctor.Body.GetILProcessor();
                il.Append(il.Create(OpCodes.Ret));
            }

            var ilProcessor = cctor.Body.GetILProcessor();
            var firstInstr = cctor.Body.Instructions.First();

            // Initialize list: m_iSubtitleSizeList = 18 to 96 step 4
            var intType = assembly.MainModule.TypeSystem.Int32;
            var sizesList = new List<int>();
            for (int s = 18; s <= 96; s += 4) sizesList.Add(s);
            var sizes = sizesList.ToArray();

            var instructions = new List<Instruction>();
            instructions.Add(ilProcessor.Create(OpCodes.Ldc_I4, sizes.Length));
            instructions.Add(ilProcessor.Create(OpCodes.Newarr, intType));
            instructions.Add(ilProcessor.Create(OpCodes.Stsfld, listField));

            for (int i = 0; i < sizes.Length; i++)
            {
                instructions.Add(ilProcessor.Create(OpCodes.Ldsfld, listField));
                instructions.Add(ilProcessor.Create(OpCodes.Ldc_I4, i));
                instructions.Add(ilProcessor.Create(OpCodes.Ldc_I4, sizes[i]));
                instructions.Add(ilProcessor.Create(OpCodes.Stelem_I4));
            }

            // Initialize default size: m_iSubtitleSize = 24
            instructions.Add(ilProcessor.Create(OpCodes.Ldc_I4, 24));
            instructions.Add(ilProcessor.Create(OpCodes.Stsfld, sizeField));

            foreach (var instr in instructions)
            {
                ilProcessor.InsertBefore(firstInstr, instr);
            }
            
            Console.WriteLine($"  ✓ Initialized m_iSubtitleSizeList in .cctor ({sizes.Length} items)");
        }

        static void CreateGetSubtitleSizeMethod(AssemblyDefinition assembly, TypeDefinition settingsClass, FieldDefinition subtitleSizeField)
        {
            var method = new MethodDefinition(
                "GetSubtitleSize",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                assembly.MainModule.TypeSystem.Int32
            );
            settingsClass.Methods.Add(method);

            var profileManagerType = assembly.MainModule.Types.First(t => t.Name == "ProfileManager");
            var getPrefIntMethod = FindMethod(profileManagerType, "GetPrefInt", 2);
            var getPrefIntRef = assembly.MainModule.ImportReference(getPrefIntMethod);

            var il = method.Body.GetILProcessor();
            
            // var val = ProfileManager.GetPrefInt("SubtitleSize", true);
            il.Append(il.Create(OpCodes.Ldstr, "SubtitleSize"));
            il.Append(il.Create(OpCodes.Ldc_I4_1)); // true
            il.Append(il.Create(OpCodes.Call, getPrefIntRef));
            
            // if (val == 0) return 24;
            var valVar = new VariableDefinition(assembly.MainModule.TypeSystem.Int32);
            method.Body.Variables.Add(valVar);
            il.Append(il.Create(OpCodes.Stloc, valVar));
            il.Append(il.Create(OpCodes.Ldloc, valVar));
            
            var returnValLabel = il.Create(OpCodes.Ldloc, valVar);
            il.Append(il.Create(OpCodes.Brtrue, returnValLabel));
            
            il.Append(il.Create(OpCodes.Ldc_I4, 24));
            il.Append(il.Create(OpCodes.Ret));
            
            il.Append(returnValLabel);
            il.Append(il.Create(OpCodes.Ret));

            Console.WriteLine("  ✓ Created GetSubtitleSize method (using ProfileManager)");
        }

        static void CreateSetSubtitleSizeMethod(AssemblyDefinition assembly, TypeDefinition settingsClass, FieldDefinition subtitleSizeField, FieldDefinition subtitleSizeListField)
        {
            var method = new MethodDefinition(
                "SetSubtitleSize",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                assembly.MainModule.TypeSystem.Void
            );
            method.Parameters.Add(new ParameterDefinition("iIndex", ParameterAttributes.None, assembly.MainModule.TypeSystem.Int32));
            settingsClass.Methods.Add(method);

            var profileManagerType = assembly.MainModule.Types.First(t => t.Name == "ProfileManager");
            var setPrefIntMethod = FindMethod(profileManagerType, "SetPrefInt", 5);
            var setPrefIntRef = assembly.MainModule.ImportReference(setPrefIntMethod);

            var il = method.Body.GetILProcessor();

            // ProfileManager.SetPrefInt("SubtitleSize", m_iSubtitleSizeList[iIndex], true, true, true)
            il.Append(il.Create(OpCodes.Ldstr, "SubtitleSize"));
            
            // m_iSubtitleSizeList[iIndex]
            il.Append(il.Create(OpCodes.Ldsfld, subtitleSizeListField));
            il.Append(il.Create(OpCodes.Ldarg_0)); 
            il.Append(il.Create(OpCodes.Ldelem_I4));
            
            il.Append(il.Create(OpCodes.Ldc_I4_1)); // true
            il.Append(il.Create(OpCodes.Ldc_I4_1)); // true
            il.Append(il.Create(OpCodes.Ldc_I4_1)); // true
            il.Append(il.Create(OpCodes.Call, setPrefIntRef));

            // Also update the static field just in case
            il.Append(il.Create(OpCodes.Ldsfld, subtitleSizeListField));
            il.Append(il.Create(OpCodes.Ldarg_0)); 
            il.Append(il.Create(OpCodes.Ldelem_I4));
            il.Append(il.Create(OpCodes.Stsfld, subtitleSizeField));

            il.Append(il.Create(OpCodes.Ret));

            Console.WriteLine("  ✓ Created SetSubtitleSize method (using ProfileManager)");
        }

        static void CreateGetSubtitleSizeListMethod(AssemblyDefinition assembly, TypeDefinition settingsClass, FieldDefinition subtitleSizeListField)
        {
            var method = new MethodDefinition(
                "GetSubtitleSizeList",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                assembly.MainModule.ImportReference(typeof(int[]))
            );
            settingsClass.Methods.Add(method);

            var il = method.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldsfld, subtitleSizeListField));
            il.Append(il.Create(OpCodes.Ret));

            Console.WriteLine("  ✓ Created GetSubtitleSizeList method");
        }

        static void PatchSettingsMenu(AssemblyDefinition assembly)
        {
            var settingsMenuClass = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "SettingsMenu");
            if (settingsMenuClass == null)
            {
                Console.WriteLine("  WARNING: SettingsMenu class not found!");
                return;
            }

            // Add m_pSubtitleSizeButton field
            var guiCompoundButtonType = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "GuiCompoundButton");
            if (guiCompoundButtonType == null)
            {
                Console.WriteLine("  WARNING: GuiCompoundButton type not found!");
                return;
            }

            var subtitleSizeButtonField = new FieldDefinition(
                "m_pSubtitleSizeButton",
                FieldAttributes.Private,
                guiCompoundButtonType
            );
            settingsMenuClass.Fields.Add(subtitleSizeButtonField);
            Console.WriteLine("  ✓ Added m_pSubtitleSizeButton field");

            // Patch BuildVideoPage to add subtitle size button UI
            PatchBuildVideoPage(assembly, settingsMenuClass, subtitleSizeButtonField);

            // Patch BuildMenuOptions to populate subtitle size options
            PatchBuildMenuOptions(assembly, settingsMenuClass, subtitleSizeButtonField);

            // Patch ApplySettings to save subtitle size
            PatchApplySettings(assembly, settingsMenuClass, subtitleSizeButtonField);

            // Patch SetMenuDataFromSettings to load subtitle size
            PatchSetMenuDataFromSettings(assembly, settingsMenuClass, subtitleSizeButtonField);
        }

        static void PatchBuildVideoPage(AssemblyDefinition assembly, TypeDefinition settingsMenuClass, FieldDefinition subtitleSizeButtonField)
        {
            var buildVideoPageMethod = settingsMenuClass.Methods.FirstOrDefault(m => m.Name == "BuildVideoPage");
            if (buildVideoPageMethod == null)
            {
                Console.WriteLine("  WARNING: BuildVideoPage method not found!");
                return;
            }

            var il = buildVideoPageMethod.Body.GetILProcessor();
            var instructions = buildVideoPageMethod.Body.Instructions;

            // Find the instruction after FOV button creation (IL_02F3: stloc.1)
            // This is where Y position is updated after FOV button
            Instruction? insertAfter = null;
            for (int i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                // Look for the pattern after FOV button:
                // ldloc.1, ldarg.0, ldfld m_pFOVButton, callvirt GetHeight, conv.i4, ldsfld OPTION_BUTTON_SPACING, add, add, stloc.1
                if (instruction.OpCode == OpCodes.Stloc_1 && i > 0)
                {
                    // Check if previous instructions match FOV button pattern
                    var prev1 = instructions[i - 1];
                    var prev2 = instructions[i - 2];
                    if (prev1.OpCode == OpCodes.Add && prev2.OpCode == OpCodes.Add)
                    {
                        // Check if this is after m_pFOVButton
                        for (int j = Math.Max(0, i - 10); j < i; j++)
                        {
                            if (instructions[j].OpCode == OpCodes.Ldfld &&
                                instructions[j].Operand is FieldReference fld &&
                                fld.Name == "m_pFOVButton")
                            {
                                insertAfter = instruction;
                                break;
                            }
                        }
                        if (insertAfter != null) break;
                    }
                }
            }

            if (insertAfter == null)
            {
                Console.WriteLine("  WARNING: Could not find FOV button insertion point!");
                return;
            }

            Console.WriteLine($"  ✓ Found insertion point at IL_{insertAfter.Offset:X4}");

            // Now inject the subtitle size button IL code
            InjectSubtitleSizeButtonIL(assembly, settingsMenuClass, buildVideoPageMethod, il, insertAfter, subtitleSizeButtonField);
        }

        static MethodDefinition FindMethod(TypeDefinition startType, string methodName, int? paramCount = null)
        {
            var currentType = startType;
            while (currentType != null)
            {
                var method = currentType.Methods.FirstOrDefault(m => 
                    m.Name == methodName && 
                    (!paramCount.HasValue || m.Parameters.Count == paramCount.Value));
                
                if (method != null) return method;

                if (currentType.BaseType == null) break;
                try 
                {
                    currentType = currentType.BaseType.Resolve();
                }
                catch
                {
                    // Could not resolve base type
                    break;
                }
            }
            return null;
        }

        static void InjectSubtitleSizeButtonIL(AssemblyDefinition assembly, TypeDefinition settingsMenuClass, MethodDefinition method, ILProcessor il, Instruction insertAfter, FieldDefinition subtitleSizeButtonField)
        {
            // Get required types and methods
            var guiCompoundButtonType = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "GuiCompoundButton");
            if (guiCompoundButtonType == null)
            {
                Console.WriteLine("  WARNING: GuiCompoundButton type not found!");
                return;
            }

            var guiCompoundButtonCtor = FindMethod(guiCompoundButtonType, ".ctor", 1);
            if (guiCompoundButtonCtor == null) { Console.WriteLine("  ERROR: GuiCompoundButton constructor not found!"); return; }

            var setNameMethod = FindMethod(guiCompoundButtonType, "SetName");
            if (setNameMethod == null) { Console.WriteLine("  ERROR: SetName method not found!"); return; }

            var setTypeMethod = FindMethod(guiCompoundButtonType, "SetType");
            if (setTypeMethod == null) { Console.WriteLine("  ERROR: SetType method not found!"); return; }

            var setCallbackMethod = FindMethod(guiCompoundButtonType, "SetCallback");
            if (setCallbackMethod == null) { Console.WriteLine("  ERROR: SetCallback method not found!"); return; }

            var setTextMethod = FindMethod(guiCompoundButtonType, "SetText");
            if (setTextMethod == null) { Console.WriteLine("  ERROR: SetText method not found!"); return; }

            var setPositionMethod = FindMethod(guiCompoundButtonType, "SetPosition", 3);
            if (setPositionMethod == null) { Console.WriteLine("  ERROR: SetPosition method not found!"); return; }

            var getHeightMethod = FindMethod(guiCompoundButtonType, "GetHeight");
            if (getHeightMethod == null) { Console.WriteLine("  ERROR: GetHeight method not found!"); return; }

            // Import methods
            var setNameMethodRef = assembly.MainModule.ImportReference(setNameMethod);
            var setCallbackMethodRef = assembly.MainModule.ImportReference(setCallbackMethod);
            var setTextMethodRef = assembly.MainModule.ImportReference(setTextMethod);
            var setPositionMethodRef = assembly.MainModule.ImportReference(setPositionMethod);
            var setTypeMethodRef = assembly.MainModule.ImportReference(setTypeMethod);
            var getHeightMethodRef = assembly.MainModule.ImportReference(getHeightMethod);
            var guiCompoundButtonCtorRef = assembly.MainModule.ImportReference(guiCompoundButtonCtor);

            // Note: We're not using Strings::GetValue for now, just hardcoded text

            // Get NL_SUBTITLE_SIZE static field (we'll need to create this)
            var nlSubtitleSizeField = new FieldDefinition(
                "NL_SUBTITLE_SIZE",
                FieldAttributes.Private | FieldAttributes.Static,
                assembly.MainModule.TypeSystem.String
            );
            settingsMenuClass.Fields.Add(nlSubtitleSizeField);

            // Get OPTION_BUTTON_SPACING field
            var optionButtonSpacingField = settingsMenuClass.Fields.FirstOrDefault(f => f.Name == "OPTION_BUTTON_SPACING");
            if (optionButtonSpacingField == null)
            {
                Console.WriteLine("  WARNING: OPTION_BUTTON_SPACING field not found!");
                return;
            }

            // Get local variable indices (loc.0 = X position, loc.1 = Y position)
            // These are already defined in the method

            var newInstructions = new List<Instruction>();

            // this.m_pSubtitleSizeButton = new GuiCompoundButton(ButtonType.optionStepper = 0)
            newInstructions.Add(il.Create(OpCodes.Ldarg_0));
            newInstructions.Add(il.Create(OpCodes.Ldc_I4_0));
            newInstructions.Add(il.Create(OpCodes.Newobj, guiCompoundButtonCtorRef));
            newInstructions.Add(il.Create(OpCodes.Stfld, subtitleSizeButtonField));

            // this.m_pSubtitleSizeButton.SetName("SubtitleSize")
            newInstructions.Add(il.Create(OpCodes.Ldarg_0));
            newInstructions.Add(il.Create(OpCodes.Ldfld, subtitleSizeButtonField));
            newInstructions.Add(il.Create(OpCodes.Ldstr, "SubtitleSize"));
            newInstructions.Add(il.Create(OpCodes.Callvirt, setNameMethodRef));

            // this.m_pSubtitleSizeButton.SetCallback("SubtitleSize")
            newInstructions.Add(il.Create(OpCodes.Ldarg_0));
            newInstructions.Add(il.Create(OpCodes.Ldfld, subtitleSizeButtonField));
            newInstructions.Add(il.Create(OpCodes.Ldstr, "SubtitleSize"));
            newInstructions.Add(il.Create(OpCodes.Callvirt, setCallbackMethodRef));

            // this.m_pSubtitleSizeButton.SetText("Subtitle Size")
            newInstructions.Add(il.Create(OpCodes.Ldarg_0));
            newInstructions.Add(il.Create(OpCodes.Ldfld, subtitleSizeButtonField));
            newInstructions.Add(il.Create(OpCodes.Ldstr, "Subtitle Size"));
            newInstructions.Add(il.Create(OpCodes.Callvirt, setTextMethodRef));

            // this.m_pSubtitleSizeButton.SetPosition(loc.0, loc.1, true)
            newInstructions.Add(il.Create(OpCodes.Ldarg_0));
            newInstructions.Add(il.Create(OpCodes.Ldfld, subtitleSizeButtonField));
            newInstructions.Add(il.Create(OpCodes.Ldloc_0));
            newInstructions.Add(il.Create(OpCodes.Conv_R4));
            newInstructions.Add(il.Create(OpCodes.Ldloc_1));
            newInstructions.Add(il.Create(OpCodes.Conv_R4));
            newInstructions.Add(il.Create(OpCodes.Ldc_I4_1));
            newInstructions.Add(il.Create(OpCodes.Callvirt, setPositionMethodRef));

            // this.m_pSubtitleSizeButton.SetType(ButtonType.optionStepper = 0)
            newInstructions.Add(il.Create(OpCodes.Ldarg_0));
            newInstructions.Add(il.Create(OpCodes.Ldfld, subtitleSizeButtonField));
            newInstructions.Add(il.Create(OpCodes.Ldc_I4_0));
            newInstructions.Add(il.Create(OpCodes.Callvirt, setTypeMethodRef));

            // loc.1 += this.m_pSubtitleSizeButton.GetHeight() + OPTION_BUTTON_SPACING
            newInstructions.Add(il.Create(OpCodes.Ldloc_1));
            newInstructions.Add(il.Create(OpCodes.Ldarg_0));
            newInstructions.Add(il.Create(OpCodes.Ldfld, subtitleSizeButtonField));
            newInstructions.Add(il.Create(OpCodes.Callvirt, getHeightMethodRef));
            newInstructions.Add(il.Create(OpCodes.Conv_I4));
            newInstructions.Add(il.Create(OpCodes.Ldsfld, optionButtonSpacingField));
            newInstructions.Add(il.Create(OpCodes.Add));
            newInstructions.Add(il.Create(OpCodes.Add));
            newInstructions.Add(il.Create(OpCodes.Stloc_1));

            // Insert all instructions after the FOV button code
            foreach (var instr in newInstructions)
            {
                il.InsertAfter(insertAfter, instr);
                insertAfter = instr;
            }

            Console.WriteLine($"  ✓ Injected subtitle size button IL (inserted {newInstructions.Count} instructions)");

            // Now we need to add AddChild and List.Add calls at the end
            PatchAddChildCalls(assembly, settingsMenuClass, method, il, subtitleSizeButtonField);
        }

        static void PatchAddChildCalls(AssemblyDefinition assembly, TypeDefinition settingsMenuClass, MethodDefinition method, ILProcessor il, FieldDefinition subtitleSizeButtonField)
        {
            // Find the AddChild calls at the end and add one for subtitle size button
            // Pattern: ldarg.0, ldarg.0, ldfld m_pFOVButton, callvirt AddChild
            // We need to add: ldarg.0, ldarg.0, ldfld m_pSubtitleSizeButton, callvirt AddChild
            // right after FOV's AddChild

            var instructions = method.Body.Instructions;
            var guiBaseType = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "GuiBase");
            if (guiBaseType == null)
            {
                Console.WriteLine("  WARNING: GuiBase type not found!");
                return;
            }

            var addChildMethod = guiBaseType.Methods.FirstOrDefault(m => m.Name == "AddChild");
            if (addChildMethod == null)
            {
                Console.WriteLine("  WARNING: AddChild method not found!");
                return;
            }
            var addChildMethodRef = assembly.MainModule.ImportReference(addChildMethod);

            Instruction? fovAddChildCall = null;
            for (int i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                if (instruction.OpCode == OpCodes.Callvirt &&
                    instruction.Operand is MethodReference method2 &&
                    method2.Name == "AddChild")
                {
                    // Check if previous instruction loads m_pFOVButton
                    if (i >= 2 && instructions[i - 1].OpCode == OpCodes.Ldfld &&
                        instructions[i - 1].Operand is FieldReference fld &&
                        fld.Name == "m_pFOVButton")
                    {
                        fovAddChildCall = instruction;
                        break;
                    }
                }
            }

            if (fovAddChildCall != null)
            {
                // Insert after FOV AddChild: this.AddChild(this.m_pSubtitleSizeButton)
                var newInstr1 = il.Create(OpCodes.Ldarg_0);
                var newInstr2 = il.Create(OpCodes.Ldarg_0);
                var newInstr3 = il.Create(OpCodes.Ldfld, subtitleSizeButtonField);
                var newInstr4 = il.Create(OpCodes.Callvirt, addChildMethodRef);

                il.InsertAfter(fovAddChildCall, newInstr1);
                il.InsertAfter(newInstr1, newInstr2);
                il.InsertAfter(newInstr2, newInstr3);
                il.InsertAfter(newInstr3, newInstr4);

                Console.WriteLine("  ✓ Added AddChild call for subtitle size button");
            }

            // Find m_pVideoPage.Add(m_pFOVButton) and add subtitle size button after it
            var listType = assembly.MainModule.ImportReference(typeof(System.Collections.Generic.List<>));
            Instruction? fovListAddCall = null;

            for (int i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                if (instruction.OpCode == OpCodes.Callvirt &&
                    instruction.Operand is MethodReference method3 &&
                    method3.Name == "Add")
                {
                    // Check if previous instruction loads m_pFOVButton
                    if (i >= 3 && instructions[i - 1].OpCode == OpCodes.Ldfld &&
                        instructions[i - 1].Operand is FieldReference fld2 &&
                        fld2.Name == "m_pFOVButton")
                    {
                        fovListAddCall = instruction;
                        break;
                    }
                }
            }

            if (fovListAddCall != null)
            {
                // Get m_pVideoPage field
                var videoPageField = settingsMenuClass.Fields.FirstOrDefault(f => f.Name == "m_pVideoPage");
                if (videoPageField == null)
                {
                    Console.WriteLine("  WARNING: m_pVideoPage field not found!");
                    return;
                }

                // Get the Add method reference from the previous call
                var addMethodRef = (MethodReference)fovListAddCall.Operand;

                // Insert: this.m_pVideoPage.Add(this.m_pSubtitleSizeButton)
                var newInstr1 = il.Create(OpCodes.Ldarg_0);
                var newInstr2 = il.Create(OpCodes.Ldfld, videoPageField);
                var newInstr3 = il.Create(OpCodes.Ldarg_0);
                var newInstr4 = il.Create(OpCodes.Ldfld, subtitleSizeButtonField);
                var newInstr5 = il.Create(OpCodes.Callvirt, addMethodRef);

                il.InsertAfter(fovListAddCall, newInstr1);
                il.InsertAfter(newInstr1, newInstr2);
                il.InsertAfter(newInstr2, newInstr3);
                il.InsertAfter(newInstr3, newInstr4);
                il.InsertAfter(newInstr4, newInstr5);

                Console.WriteLine("  ✓ Added m_pVideoPage.Add call for subtitle size button");
            }
        }

        static void PatchBuildMenuOptions(AssemblyDefinition assembly, TypeDefinition settingsMenuClass, FieldDefinition subtitleSizeButtonField)
        {
            var method = settingsMenuClass.Methods.FirstOrDefault(m => m.Name == "BuildMenuOptions");
            if (method == null) { Console.WriteLine("  WARNING: BuildMenuOptions method not found!"); return; }

            // Find Settings class and methods
            var settingsClass = assembly.MainModule.Types.First(t => t.Name == "Settings");
            var getSubtitleSizeListMethod = settingsClass.Methods.First(m => m.Name == "GetSubtitleSizeList");

            // Find GuiCompoundButton.GetOptionStepper
            var guiCompoundButtonType = assembly.MainModule.Types.First(t => t.Name == "GuiCompoundButton");
            var getOptionStepperMethod = FindMethod(guiCompoundButtonType, "GetOptionStepper");
            
            // Find OptionStepper.AddOption
            var optionStepperType = assembly.MainModule.Types.First(t => t.Name == "OptionStepper");
            var addOptionMethod = FindMethod(optionStepperType, "AddOption", 1);

            // Import references
            var getSubtitleSizeListRef = assembly.MainModule.ImportReference(getSubtitleSizeListMethod);
            var getOptionStepperRef = assembly.MainModule.ImportReference(getOptionStepperMethod);
            var addOptionRef = assembly.MainModule.ImportReference(addOptionMethod);
            var intToStringRef = assembly.MainModule.ImportReference(assembly.MainModule.TypeSystem.Int32.Resolve().Methods.First(m => m.Name == "ToString" && m.Parameters.Count == 0));

            var il = method.Body.GetILProcessor();
            var lastRet = method.Body.Instructions.Last(); 

            // Variables
            var sizesVar = new VariableDefinition(assembly.MainModule.ImportReference(typeof(int[])));
            method.Body.Variables.Add(sizesVar);
            var iVar = new VariableDefinition(assembly.MainModule.TypeSystem.Int32);
            method.Body.Variables.Add(iVar);

            // sizes = Settings.GetSubtitleSizeList()
            il.InsertBefore(lastRet, il.Create(OpCodes.Call, getSubtitleSizeListRef));
            il.InsertBefore(lastRet, il.Create(OpCodes.Stloc, sizesVar));

            // for (i = 0; i < sizes.Length; i++)
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldc_I4_0));
            il.InsertBefore(lastRet, il.Create(OpCodes.Stloc, iVar));

            var loopHead = il.Create(OpCodes.Ldloc, iVar);
            var loopBody = il.Create(OpCodes.Ldarg_0); // start of body

            il.InsertBefore(lastRet, il.Create(OpCodes.Br, loopHead));

            // Loop body
            // this.m_pSubtitleSizeButton.GetOptionStepper().AddOption(sizes[i].ToString())
            il.InsertBefore(lastRet, loopBody);
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldfld, subtitleSizeButtonField));
            il.InsertBefore(lastRet, il.Create(OpCodes.Callvirt, getOptionStepperRef));
            
            // Load array element address to call ToString on it (or load value and box? ToString is on value type)
            // Ldelema is better for ToString call on struct
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldloc, sizesVar));
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldloc, iVar));
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldelema, assembly.MainModule.TypeSystem.Int32));
            il.InsertBefore(lastRet, il.Create(OpCodes.Call, intToStringRef));
            
            il.InsertBefore(lastRet, il.Create(OpCodes.Callvirt, addOptionRef));

            // i++
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldloc, iVar));
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldc_I4_1));
            il.InsertBefore(lastRet, il.Create(OpCodes.Add));
            il.InsertBefore(lastRet, il.Create(OpCodes.Stloc, iVar));

            // Loop condition
            il.InsertBefore(lastRet, loopHead);
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldloc, sizesVar));
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldlen));
            il.InsertBefore(lastRet, il.Create(OpCodes.Conv_I4));
            il.InsertBefore(lastRet, il.Create(OpCodes.Blt, loopBody));

            Console.WriteLine("  ✓ Patched BuildMenuOptions to populate subtitle sizes");
        }

        static void PatchApplySettings(AssemblyDefinition assembly, TypeDefinition settingsMenuClass, FieldDefinition subtitleSizeButtonField)
        {
            var method = settingsMenuClass.Methods.FirstOrDefault(m => m.Name == "ApplySettings");
            if (method == null) { Console.WriteLine("  WARNING: ApplySettings method not found!"); return; }

            // Find Settings.SetSubtitleSize
            var settingsClass = assembly.MainModule.Types.First(t => t.Name == "Settings");
            var setSubtitleSizeMethod = settingsClass.Methods.First(m => m.Name == "SetSubtitleSize");

             // Find GuiCompoundButton.GetOptionStepper
            var guiCompoundButtonType = assembly.MainModule.Types.First(t => t.Name == "GuiCompoundButton");
            var getOptionStepperMethod = FindMethod(guiCompoundButtonType, "GetOptionStepper");

            // Find OptionStepper.GetCurrentOptionIndex
            var optionStepperType = assembly.MainModule.Types.First(t => t.Name == "OptionStepper");
            var getCurrentOptionIndexMethod = FindMethod(optionStepperType, "GetCurrentOptionIndex");

            // Import references
            var setSubtitleSizeRef = assembly.MainModule.ImportReference(setSubtitleSizeMethod);
            var getOptionStepperRef = assembly.MainModule.ImportReference(getOptionStepperMethod);
            var getCurrentOptionIndexRef = assembly.MainModule.ImportReference(getCurrentOptionIndexMethod);

            // Insert at beginning of method (simpler than finding exact spot, Settings.Set... order doesn't matter much)
            var il = method.Body.GetILProcessor();
            var firstInstr = method.Body.Instructions.First();

            // Settings.SetSubtitleSize(this.m_pSubtitleSizeButton.GetOptionStepper().GetCurrentOptionIndex())
            il.InsertBefore(firstInstr, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(firstInstr, il.Create(OpCodes.Ldfld, subtitleSizeButtonField));
            il.InsertBefore(firstInstr, il.Create(OpCodes.Callvirt, getOptionStepperRef));
            il.InsertBefore(firstInstr, il.Create(OpCodes.Callvirt, getCurrentOptionIndexRef));
            il.InsertBefore(firstInstr, il.Create(OpCodes.Call, setSubtitleSizeRef));

            Console.WriteLine("  ✓ Patched ApplySettings to save subtitle size");
        }

        static void PatchSetMenuDataFromSettings(AssemblyDefinition assembly, TypeDefinition settingsMenuClass, FieldDefinition subtitleSizeButtonField)
        {
            var method = settingsMenuClass.Methods.FirstOrDefault(m => m.Name == "SetMenuDataFromSettings");
            if (method == null) { Console.WriteLine("  WARNING: SetMenuDataFromSettings method not found!"); return; }

            // Find Settings methods
            var settingsClass = assembly.MainModule.Types.First(t => t.Name == "Settings");
            var getSubtitleSizeMethod = settingsClass.Methods.First(m => m.Name == "GetSubtitleSize");
            var getSubtitleSizeListMethod = settingsClass.Methods.First(m => m.Name == "GetSubtitleSizeList");

            // Find UI methods
            var guiCompoundButtonType = assembly.MainModule.Types.First(t => t.Name == "GuiCompoundButton");
            var getOptionStepperMethod = FindMethod(guiCompoundButtonType, "GetOptionStepper");
            var optionStepperType = assembly.MainModule.Types.First(t => t.Name == "OptionStepper");
            var setCurrentOptionMethod = FindMethod(optionStepperType, "SetCurrentOption", 1);

            // Import references
            var getSubtitleSizeRef = assembly.MainModule.ImportReference(getSubtitleSizeMethod);
            var getSubtitleSizeListRef = assembly.MainModule.ImportReference(getSubtitleSizeListMethod);
            var getOptionStepperRef = assembly.MainModule.ImportReference(getOptionStepperMethod);
            var setCurrentOptionRef = assembly.MainModule.ImportReference(setCurrentOptionMethod);

            var il = method.Body.GetILProcessor();
            var lastRet = method.Body.Instructions.Last();

            // Variables
            var currentSizeVar = new VariableDefinition(assembly.MainModule.TypeSystem.Int32);
            method.Body.Variables.Add(currentSizeVar);
            var sizesVar = new VariableDefinition(assembly.MainModule.ImportReference(typeof(int[])));
            method.Body.Variables.Add(sizesVar);
            var iVar = new VariableDefinition(assembly.MainModule.TypeSystem.Int32);
            method.Body.Variables.Add(iVar);

            // currentSize = Settings.GetSubtitleSize()
            il.InsertBefore(lastRet, il.Create(OpCodes.Call, getSubtitleSizeRef));
            il.InsertBefore(lastRet, il.Create(OpCodes.Stloc, currentSizeVar));

            // sizes = Settings.GetSubtitleSizeList()
            il.InsertBefore(lastRet, il.Create(OpCodes.Call, getSubtitleSizeListRef));
            il.InsertBefore(lastRet, il.Create(OpCodes.Stloc, sizesVar));

            // for (i = 0; i < sizes.Length; i++)
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldc_I4_0));
            il.InsertBefore(lastRet, il.Create(OpCodes.Stloc, iVar));

            var loopHead = il.Create(OpCodes.Ldloc, iVar);
            var loopBody = il.Create(OpCodes.Ldloc, sizesVar); // start of body: if (sizes[i] == currentSize)

            il.InsertBefore(lastRet, il.Create(OpCodes.Br, loopHead));

            // Loop body
            // if (sizes[i] == currentSize)
            il.InsertBefore(lastRet, loopBody);
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldloc, iVar));
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldelem_I4));
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldloc, currentSizeVar));
            var nextIter = il.Create(OpCodes.Nop);
            il.InsertBefore(lastRet, il.Create(OpCodes.Bne_Un, nextIter));

            // this.m_pSubtitleSizeButton.GetOptionStepper().SetCurrentOption(i)
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldfld, subtitleSizeButtonField));
            il.InsertBefore(lastRet, il.Create(OpCodes.Callvirt, getOptionStepperRef));
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldloc, iVar));
            il.InsertBefore(lastRet, il.Create(OpCodes.Callvirt, setCurrentOptionRef));
            
            // break (jump to end, which is lastRet)
            il.InsertBefore(lastRet, il.Create(OpCodes.Br, lastRet));

            // next iteration
            il.InsertBefore(lastRet, nextIter);
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldloc, iVar));
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldc_I4_1));
            il.InsertBefore(lastRet, il.Create(OpCodes.Add));
            il.InsertBefore(lastRet, il.Create(OpCodes.Stloc, iVar));

            // Loop condition
            il.InsertBefore(lastRet, loopHead);
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldloc, sizesVar));
            il.InsertBefore(lastRet, il.Create(OpCodes.Ldlen));
            il.InsertBefore(lastRet, il.Create(OpCodes.Conv_I4));
            il.InsertBefore(lastRet, il.Create(OpCodes.Blt, loopBody));

            Console.WriteLine("  ✓ Patched SetMenuDataFromSettings to load subtitle size");
        }
    }
}
