using InventoryPOS.Licensing;

// Activation keys for the desktop register. Only the vendor runs this, on their own computer, with the private key.
//
//   licencias llaves     [--privada <archivo>]                  Crea el par de llaves (una sola vez).
//   licencias activar    --codigo <código del equipo> --cliente "<nombre>" [--vence AAAA-MM-DD] [--privada <archivo>] [--salida <archivo>]
//   licencias revisar    --clave <clave o archivo> --codigo <código del equipo> [--publica <llave>]

const string DefaultPrivate = "llave-privada-licencias.txt";

try
{
    return args.FirstOrDefault()?.ToLowerInvariant() switch
    {
        "llaves" => KeyPair(Opt("--privada") ?? DefaultPrivate),
        "activar" => Issue(),
        "revisar" => Inspect(),
        _ => Help(),
    };
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or FormatException
                              or System.Security.Cryptography.CryptographicException)
{
    Console.Error.WriteLine("Error: " + ex.Message);
    return 1;
}

int KeyPair(string file)
{
    if (File.Exists(file)) throw new InvalidOperationException($"{file} ya existe. No lo reemplaces: las claves ya emitidas dejarían de servir.");
    var (privateKey, publicKey) = LicenseKey.CreateKeyPair();
    File.WriteAllText(file, privateKey + Environment.NewLine);
    Console.WriteLine($"Llave privada guardada en {Path.GetFullPath(file)}");
    Console.WriteLine("  Guárdala en un lugar seguro y con respaldo. NO la subas al repositorio ni la compartas:");
    Console.WriteLine("  con ella cualquiera podría activar el programa en las computadoras que quiera.");
    Console.WriteLine();
    Console.WriteLine("Llave pública (pégala en InventoryPOS.Desktop/LicensePublicKey.cs antes de publicar):");
    Console.WriteLine(publicKey);
    return 0;
}

int Issue()
{
    var codeText = Opt("--codigo") ?? throw new ArgumentException("Falta --codigo (el código que muestra la caja).");
    if (!MachineCode.TryParse(codeText, out var code)) throw new ArgumentException("El código del equipo está mal copiado; revísalo con el cliente.");
    var customer = Opt("--cliente") ?? throw new ArgumentException("Falta --cliente.");
    DateOnly? expires = Opt("--vence") is { } v ? DateOnly.ParseExact(v, "yyyy-MM-dd") : null;
    var privateKey = File.ReadAllText(Opt("--privada") ?? DefaultPrivate);
    var key = LicenseKey.Issue(privateKey, customer, code, DateOnly.FromDateTime(DateTime.Today), expires);

    var output = Opt("--salida");
    if (output != null) File.WriteAllText(output, key + Environment.NewLine);
    Console.WriteLine($"Clave de activación para {customer}, equipo {code}{(expires is { } e ? $", vence {e:dd/MM/yyyy}" : "")}:");
    Console.WriteLine();
    Console.WriteLine(key);
    if (output != null) Console.WriteLine($"\nTambién guardada en {Path.GetFullPath(output)} (el cliente puede cargar ese archivo).");
    return 0;
}

int Inspect()
{
    var keyArg = Opt("--clave") ?? throw new ArgumentException("Falta --clave.");
    var key = File.Exists(keyArg) ? File.ReadAllText(keyArg) : keyArg;
    if (!MachineCode.TryParse(Opt("--codigo"), out var code)) throw new ArgumentException("Falta --codigo o está mal copiado.");
    var publicKey = Opt("--publica") ?? LicenseKey.PublicKeyOf(File.ReadAllText(Opt("--privada") ?? DefaultPrivate));
    var check = LicenseKey.Check(key, publicKey, code, DateOnly.FromDateTime(DateTime.Today));
    Console.WriteLine(check.Message);
    if (check.License is { } l) Console.WriteLine($"Cliente: {l.Customer} · Equipo: {l.Machine} · Emitida: {l.IssuedOn:dd/MM/yyyy} · Folio: {l.Id}");
    return check.IsValid ? 0 : 2;
}

int Help()
{
    Console.WriteLine("""
        Licencias del punto de venta (solo para el distribuidor del programa).

          licencias llaves                         Crea tu par de llaves, una sola vez.
          licencias activar --codigo XXXX-XXXX-…   Genera la clave de activación de una computadora.
                            --cliente "Anna’s Beauty Supply" [--vence 2027-12-31] [--salida clave.txt]
          licencias revisar --clave <clave|archivo> --codigo XXXX-XXXX-…

        La llave privada se lee de llave-privada-licencias.txt (o --privada <archivo>).
        """);
    return args.Length == 0 ? 0 : 1;
}

string? Opt(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
