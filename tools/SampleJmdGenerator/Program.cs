using JmdExplorer.Tools.SampleGenerator;

string outDir = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "samples");
Directory.CreateDirectory(outDir);

void Write(string name, byte[] bytes)
{
    string path = Path.Combine(outDir, name);
    File.WriteAllBytes(path, bytes);
    Console.WriteLine($"  wrote {name,-28} {bytes.Length,10:N0} bytes  ->  {path}");
}

Console.WriteLine("JMD Explorer — sample file generator");
Console.WriteLine("------------------------------------");
Write("carinfo.jmd", SampleFileBuilder.BuildXenonSample());
Write("unknown_blob.jmd", SampleFileBuilder.BuildUnknownSample());
Write("strings_demo.bin", SampleFileBuilder.BuildStringSample());
Console.WriteLine();
Console.WriteLine($"Done. Open these in JMD Explorer (drag & drop or Open File).");
Console.WriteLine("Note: carinfo.jmd carries a real 'Xenon Data Format v4s' header, an embedded");
Console.WriteLine("PNG signature, a 64-byte repeating record table, and a high-entropy payload.");
