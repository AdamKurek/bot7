using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bot7
{
    internal class PiperIntegration
    {
        public object o = new();
        private Process? readySpeach;
        public int modelHz = 16000;
        int GetModelHz(string jsonPath)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(jsonPath));
            if (doc.RootElement.TryGetProperty("audio", out var a) &&
                a.TryGetProperty("sample_rate", out var sr))
                return sr.GetInt32();
            return 22050; 
        }
        public Process? ReadySpeach {
            set
            {
                lock (o) { 
                    readySpeach = value;
                }
            }
            get
            {
                lock (o)
                {
                    return readySpeach;
                }
            }
        }

        public Process TextToStream(string text, string piperExePath, string modelPath)
        {
            if (!File.Exists(piperExePath))
                throw new FileNotFoundException("piper.exe not found", piperExePath);
            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Model .onnx not found", modelPath);
            var jsonPath = Path.ChangeExtension(modelPath, ".onnx.json");
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException(".onnx.json not found", jsonPath);
            modelHz = GetModelHz(jsonPath);
            if (modelHz == 0)
            {
                Console.WriteLine("Failed Reading json Hz");
                return null;
            }

            var psi = new ProcessStartInfo
            {
                FileName = piperExePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(piperExePath)
            };
#if NET6_0_OR_GREATER
            psi.ArgumentList.Add("-m"); psi.ArgumentList.Add(modelPath);
            psi.ArgumentList.Add("--output_file"); psi.ArgumentList.Add("-");
            psi.ArgumentList.Add("--output-raw"); 
#else
psi.Arguments = $"-m \"{modelPath}\" --output_file -";
#endif

            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.Start();
            if (readySpeach is object)
            {
                readySpeach.Dispose();
                readySpeach = null;
            }
            ReadySpeach = p;
            using (var stdin = p.StandardInput)
                stdin.WriteLine(text);
            var _ = Task.Run(async () =>
            {
                var err = await p.StandardError.ReadToEndAsync();
                if (p.ExitCode != 0 && !string.IsNullOrWhiteSpace(err))
                    Console.Error.WriteLine(err);
            });
            return p;

        }
        public static string TextToWav(string text, string piperExePath, string modelPath, string outputFile)
        {
            if (!File.Exists(piperExePath))
            {
                throw new FileNotFoundException("piper.exe not found", piperExePath);
            }
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException("Model .onnx not found", modelPath);
            }
            var jsonPath = Path.ChangeExtension(modelPath, ".onnx.json");
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException("Model .onnx.json not found (must be next to .onnx with same basename)", jsonPath);
            }

            var psi = new ProcessStartInfo(piperExePath, $" -m {modelPath} -f {outputFile}")
            {
                FileName = piperExePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = false,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(piperExePath)
            };


            using var p = new Process { StartInfo = psi };
            p.Start();

            // Send text + newline, then close to signal EOF
            using (var stdin = p.StandardInput)
            {
                stdin.WriteLine(text);
            }

            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            //if (p.ExitCode != 0 || !string.IsNullOrEmpty(stderr))
            //    throw new Exception($"Piper failed (code {p.ExitCode}). stderr:\n{stderr}");

            return outputFile;
        }

    }
}
