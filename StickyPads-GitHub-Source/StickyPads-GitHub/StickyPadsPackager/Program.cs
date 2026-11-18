
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

string inputBundle = Path.GetFullPath("StickyPads-Clean-Windows.bundle");
string outputBundle = Path.GetFullPath("StickyPads-Ready-Windows.bundle");
string outputMod = Path.GetFullPath("StickyPads-Test.sr2-mod");

string rawDllPath = Path.GetFullPath(
    @"bin\Debug\netstandard2.1\StickyPads.dll");

string stockXmlPath = Path.GetFullPath("Strut1-Stock.xml");

string classPackagePath = Path.GetFullPath(
    @"ClassData\uncompressed.tpk");

string gameDll =
    @"C:\Program Files (x86)\Steam\steamapps\common\SimpleRockets2\SimpleRockets2_Data\Managed\Jundroo.ModTools.Core.dll";

const string stickyPadId = "SilverExeStickyPad";
const string stickyPadName = "Sticky Pad";
const string stickyPadXmlPath =
    "Assets/Resources/Craft/Parts/Definitions/StickyPad.xml";
const string stickyPadBundlePath =
    "assets/resources/craft/parts/definitions/stickypad.xml";

const string grappleId = "SilverExeGrapple";
const string grappleName = "Grapple";
const string grappleXmlPath =
    "Assets/Resources/Craft/Parts/Definitions/Grapple.xml";
const string grappleBundlePath =
    "assets/resources/craft/parts/definitions/grapple.xml";

const string ballJointId = "SilverExeBallJoint";
const string ballJointName = "Ball Joint";
const string ballJointXmlPath =
    "Assets/Resources/Craft/Parts/Definitions/BallJoint.xml";
const string ballJointBundlePath =
    "assets/resources/craft/parts/definitions/balljoint.xml";

const string prefabPath = "Craft/Parts/Prefabs/Strut1.prefab";

Console.WriteLine("Checking input files...");

foreach (string path in new[]
{
    inputBundle,
    rawDllPath,
    stockXmlPath,
    classPackagePath,
    gameDll
})
{
    if (!File.Exists(path))
        throw new FileNotFoundException("Required file not found.", path);
}

Console.WriteLine("Loading mod API headers...");

var assembly = Assembly.LoadFrom(gameDll);

var headers = assembly.GetType(
    "Jundroo.ModTools.Core.AssetHeaders", true)!;

var assemblyHeader = (byte[])headers.GetField(
    "AssemblyHeader",
    BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

var saveAsset = headers.GetMethod(
    "SaveAsset",
    BindingFlags.Public | BindingFlags.Static)!;

var loadManifest = headers.GetMethod(
    "LoadManifest",
    BindingFlags.Public | BindingFlags.Static)!;

var saveManifest = headers.GetMethod(
    "SaveManifest",
    BindingFlags.Public | BindingFlags.Static)!;

byte[] rawDll = File.ReadAllBytes(rawDllPath);

using var dllStream = new MemoryStream();

saveAsset.Invoke(
    null,
    new object[] { dllStream, rawDll, assemblyHeader });

byte[] wrappedDll = dllStream.ToArray();

Console.WriteLine($"Raw DLL: {rawDll.Length:N0} bytes");
Console.WriteLine($"Wrapped DLL: {wrappedDll.Length:N0} bytes");

Console.WriteLine("Loading Unity bundle...");

var manager = new AssetsManager();

var bundle = manager.LoadBundleFile(inputBundle, false);
var assets = manager.LoadAssetsFileFromBundle(bundle, 0, false);

manager.LoadClassPackage(classPackagePath);
manager.LoadClassDatabaseFromPackage(assets.file.Metadata.UnityVersion);

if (manager.ClassDatabase == null)
    throw new Exception("AssetsTools class database failed to load.");

Console.WriteLine(
    $"Unity version: {assets.file.Metadata.UnityVersion}");

Console.WriteLine(
    $"Existing asset count: {assets.file.Metadata.AssetInfos.Count}");

// Replace the DLL TextAsset.

var dllInfo = assets.file.GetAssetsOfType(49).First(x =>
{
    var field = manager.GetBaseField(assets, x, AssetReadFlags.None);
    return field["m_Name"].AsString == "StickyPads.dll";
});

var dllField = manager.GetBaseField(
    assets, dllInfo, AssetReadFlags.None);

dllField["m_Script"].AsByteArray = wrappedDll;
dllInfo.SetNewData(dllField);

Console.WriteLine("StickyPads DLL asset replaced.");

// Find the manifest TextAsset.

var manifestInfo = assets.file.GetAssetsOfType(49).First(x =>
{
    var field = manager.GetBaseField(assets, x, AssetReadFlags.None);
    return field["m_Name"].AsString == "ModManifest.xml";
});

var manifestField = manager.GetBaseField(
    assets, manifestInfo, AssetReadFlags.None);

// Update the manifest.

byte[] oldManifest = manifestField["m_Script"].AsByteArray;

int bomStart = -1;

for (int i = 0; i <= oldManifest.Length - 3; i++)
{
    if (oldManifest[i] == 0xEF &&
        oldManifest[i + 1] == 0xBB &&
        oldManifest[i + 2] == 0xBF)
    {
        bomStart = i;
        break;
    }
}

if (bomStart < 0)
    throw new Exception("Manifest UTF-8 BOM not found.");

string manifestText = Encoding.UTF8.GetString(
    oldManifest.Skip(bomStart + 3).ToArray());

var manifestXml = XDocument.Parse(manifestText);

manifestXml.Root!.SetAttributeValue(
    "buildID",
    "d7c86f31-9e42-4b65-8a13-52e1b9c7f406");

var mod = manifestXml.Root.Element("Mod")
    ?? throw new Exception("Mod element not found.");

// Update the manifest timestamp.

mod.SetAttributeValue(
    "lastUpdated",
    DateTime.UtcNow.ToString("O"));

// Register the three parts.

var partsElement = mod.Element("Parts");

if (partsElement == null)
{
    partsElement = new XElement("Parts");
    mod.Add(partsElement);
}

partsElement.RemoveNodes();

partsElement.Add(
    new XElement(
        "Part",
        new XAttribute("id", stickyPadId),
        new XAttribute("xmlPath", stickyPadXmlPath),
        new XAttribute("prefabPath", prefabPath)));

partsElement.Add(
    new XElement(
        "Part",
        new XAttribute("id", grappleId),
        new XAttribute("xmlPath", grappleXmlPath),
        new XAttribute("prefabPath", prefabPath)));

partsElement.Add(
    new XElement(
        "Part",
        new XAttribute("id", ballJointId),
        new XAttribute("xmlPath", ballJointXmlPath),
        new XAttribute("prefabPath", prefabPath)));

Console.WriteLine("Sticky Pad, Grapple, and Ball Joint registered in manifest.");

// Save and validate the manifest.

byte[] newManifest = (byte[])saveManifest.Invoke(
    null, new object[] { manifestXml })!;

var manifestCheck = loadManifest.Invoke(
    null, new object[] { newManifest });

if (manifestCheck == null)
    throw new Exception("Manifest validation failed.");

manifestField["m_Script"].AsByteArray = newManifest;
manifestInfo.SetNewData(manifestField);

Console.WriteLine("Manifest updated and validated.");

// Build the part definitions.

Console.WriteLine("Building Sticky Pad part definition...");

XDocument stickyPadXml = BuildPartXml(
    stockXmlPath,
    stickyPadId,
    stickyPadName,
    "SilverExe.StickyPad",
    "A surface-gripping StickyPad.",
    true);

Console.WriteLine("Building Grapple part definition...");

XDocument grappleXml = BuildPartXml(
    stockXmlPath,
    grappleId,
    grappleName,
    "SilverExe.Grapple",
    "A launcher that fires a grappling hook.",
    false);

Console.WriteLine("Building Ball Joint part definition...");

XDocument ballJointXml = BuildPartXml(
    stockXmlPath,
    ballJointId,
    ballJointName,
    "SilverExe.BallJoint",
    "A two-axis powered ball joint.",
    false,
    true);

// Verify Grapple attachment points.

var grappleAttachPoints = grappleXml.Root?
    .Element("PartType")?
    .Element("AttachPoints")?
    .Elements("AttachPoint")
    .ToList()
    ?? throw new Exception("Grapple AttachPoints verification failed.");

if (grappleAttachPoints.Count != 2 ||
    (string?)grappleAttachPoints[1].Attribute("position") != "0,-1,0")
{
    throw new Exception(
        "Grapple must retain only the bottom Base attachment point.");
}

Console.WriteLine(
    "Grapple attachment check passed: bottom Base retained, top Tip removed.");

string stickyPadXmlText = stickyPadXml.ToString(
    SaveOptions.DisableFormatting);

string grappleXmlText = grappleXml.ToString(
    SaveOptions.DisableFormatting);

string ballJointXmlText = ballJointXml.ToString(
    SaveOptions.DisableFormatting);

Console.WriteLine(
    $"Sticky Pad XML: {Encoding.UTF8.GetByteCount(stickyPadXmlText):N0} bytes");

Console.WriteLine(
    $"Grapple XML: {Encoding.UTF8.GetByteCount(grappleXmlText):N0} bytes");

// Find the AssetBundle and its registration arrays.

var assetBundleInfo = assets.file
    .GetAssetsOfType(142)
    .First();

var assetBundleField = manager.GetBaseField(
    assets, assetBundleInfo, AssetReadFlags.None);

var preloadRoot = assetBundleField["m_PreloadTable"];
var containerRoot = assetBundleField["m_Container"];

var preloadArray = FindArrayNode(
    preloadRoot,
    entry =>
        HasChild(entry, "m_FileID") &&
        HasChild(entry, "m_PathID"));

var containerArray = FindArrayNode(
    containerRoot,
    entry =>
        HasChild(entry, "first") &&
        HasChild(entry, "second"));

if (preloadArray == null)
    throw new Exception(
        "Could not locate the AssetBundle preload array.");

if (containerArray == null)
    throw new Exception(
        "Could not locate the AssetBundle container array.");

if (preloadArray.Children.Count == 0)
    throw new Exception("Preload array is unexpectedly empty.");

if (containerArray.Children.Count == 0)
    throw new Exception("Container array is unexpectedly empty.");

// Add the three part XML TextAssets.

long nextPathId = assets.file.Metadata.AssetInfos
    .Max(x => x.PathId) + 1;

var stickyPadAsset = AddPartTextAsset(
    manager,
    assets,
    manifestField,
    preloadArray,
    containerArray,
    stickyPadXmlText,
    "StickyPad.xml",
    stickyPadBundlePath,
    nextPathId);

nextPathId++;

var grappleAsset = AddPartTextAsset(
    manager,
    assets,
    manifestField,
    preloadArray,
    containerArray,
    grappleXmlText,
    "Grapple.xml",
    grappleBundlePath,
    nextPathId);

nextPathId++;

var ballJointAsset = AddPartTextAsset(
    manager,
    assets,
    manifestField,
    preloadArray,
    containerArray,
    ballJointXmlText,
    "BallJoint.xml",
    ballJointBundlePath,
    nextPathId);

// Save the updated AssetBundle registration.

assetBundleInfo.SetNewData(assetBundleField);

Console.WriteLine(
    $"Preload table updated: {preloadArray.Children.Count} entries.");

Console.WriteLine(
    $"Container updated: {containerArray.Children.Count} entries.");

// Verify the three XML TextAssets.

VerifyPartAsset(
    manager,
    assets,
    stickyPadAsset,
    "StickyPad.xml",
    stickyPadId,
    "SilverExe.StickyPad",
    stickyPadBundlePath,
    true,
    preloadArray,
    containerArray);

VerifyPartAsset(
    manager,
    assets,
    grappleAsset,
    "Grapple.xml",
    grappleId,
    "SilverExe.Grapple",
    grappleBundlePath,
    false,
    preloadArray,
    containerArray);

VerifyPartAsset(
    manager,
    assets,
    ballJointAsset,
    "BallJoint.xml",
    ballJointId,
    "SilverExe.BallJoint",
    ballJointBundlePath,
    true,
    preloadArray,
    containerArray);

// Verify Ball Joint attachment settings.

var ballJointCheck = XDocument.Parse(ballJointXmlText);

var ballJointPartType = ballJointCheck.Root?.Element("PartType")
    ?? throw new Exception("Ball Joint PartType missing.");

var ballJointAttachPoint = ballJointPartType
    .Element("AttachPoints")?
    .Elements("AttachPoint")
    .ElementAt(2)
    ?? throw new Exception("Ball Joint Top attach point missing.");

if ((string?)ballJointAttachPoint.Attribute("jointType") != "Hinge" ||
    (string?)ballJointAttachPoint.Attribute("requiresPhysicsJoint") != "true" ||
    (string?)ballJointAttachPoint.Attribute("localJointAxis") != "1,0,0")
{
    throw new Exception("Ball Joint physics attachment verification failed.");
}

// Verify Ball Joint controller count.

var ballJointModifiers = ballJointPartType.Element("Modifiers")
    ?? throw new Exception("Ball Joint Modifiers missing.");

if (ballJointModifiers.Elements("InputController").Count() != 2)
    throw new Exception("Ball Joint must have exactly two input controllers.");

Console.WriteLine("Sticky Pad TextAsset verification passed.");
Console.WriteLine("Grapple TextAsset verification passed.");
Console.WriteLine("Ball Joint TextAsset verification passed.");
Console.WriteLine("All three part modifier verifications passed.");
Console.WriteLine("All three AssetBundle registrations passed.");

// Write the corrected Unity AssetBundle.

Console.WriteLine("Writing Unity bundle...");

bundle.file.BlockAndDirInfo.DirectoryInfos[0]
    .SetNewData(assets.file);

using (var writer = new AssetsFileWriter(outputBundle))
{
    bundle.file.Write(writer);
}

byte[] bundleBytes = File.ReadAllBytes(outputBundle);

if (Encoding.ASCII.GetString(
        bundleBytes.Take(7).ToArray()) != "UnityFS")
{
    throw new Exception("Output bundle is not UnityFS.");
}

Console.WriteLine(
    $"Corrected bundle: {bundleBytes.Length:N0} bytes");

// Build the .sr2-mod wrapper.

Console.WriteLine("Building .sr2-mod package...");

const string compressedHeader =
    "SimpleRockets2CompressedModFileV001";

const string modHeader =
    "SimpleRockets2ModHeaderV001";

using var rawStream = new MemoryStream();

using (var writer = new BinaryWriter(
    rawStream, Encoding.ASCII, true))
{
    writer.Write(Encoding.ASCII.GetBytes(modHeader));

    writer.Write(59L);
    writer.Write(-1L);
    writer.Write(-1L);
    writer.Write(-1L);

    if (rawStream.Position != 59)
        throw new Exception("Unexpected mod header length.");

    writer.Write(bundleBytes);
}

byte[] rawPackage = rawStream.ToArray();

using var compressedStream = new MemoryStream();

using (var gzip = new GZipStream(
    compressedStream,
    CompressionLevel.Optimal,
    true))
{
    gzip.Write(rawPackage);
}

byte[] gzipBytes = compressedStream.ToArray();

using (var finalStream = new FileStream(
    outputMod,
    FileMode.Create,
    FileAccess.Write))
{
    finalStream.Write(
        Encoding.ASCII.GetBytes(compressedHeader));

    finalStream.Write(gzipBytes);
}

Console.WriteLine($"Package created: {outputMod}");
Console.WriteLine(
    $"Package size: {new FileInfo(outputMod).Length:N0} bytes");

// Verify the package wrapper.

Console.WriteLine();
Console.WriteLine("=== PACKAGE VERIFICATION ===");

byte[] packageBytes = File.ReadAllBytes(outputMod);

int prefixLength = Encoding.ASCII.GetByteCount(
    compressedHeader);

if (Encoding.ASCII.GetString(
        packageBytes, 0, prefixLength) != compressedHeader)
{
    throw new Exception(
        "Compressed header verification failed.");
}

using var verifyInput = new MemoryStream(
    packageBytes,
    prefixLength,
    packageBytes.Length - prefixLength);

using var verifyGzip = new GZipStream(
    verifyInput,
    CompressionMode.Decompress);

using var verifyOutput = new MemoryStream();

verifyGzip.CopyTo(verifyOutput);

byte[] verifiedRaw = verifyOutput.ToArray();

if (Encoding.ASCII.GetString(
        verifiedRaw, 0, modHeader.Length) != modHeader)
{
    throw new Exception("Mod header verification failed.");
}

long windowsOffset = BitConverter.ToInt64(
    verifiedRaw, 27);

long macOffset = BitConverter.ToInt64(
    verifiedRaw, 35);

long linuxOffset = BitConverter.ToInt64(
    verifiedRaw, 43);

long androidOffset = BitConverter.ToInt64(
    verifiedRaw, 51);

if (windowsOffset != 59 ||
    macOffset != -1 ||
    linuxOffset != -1 ||
    androidOffset != -1)
{
    throw new Exception(
        "Platform offset verification failed.");
}

if (Encoding.ASCII.GetString(
        verifiedRaw, 59, 7) != "UnityFS")
{
    throw new Exception(
        "Embedded UnityFS verification failed.");
}

Console.WriteLine("Compressed header: OK");
Console.WriteLine("Mod header: OK");
Console.WriteLine($"Windows offset: {windowsOffset}");
Console.WriteLine($"macOS offset: {macOffset}");
Console.WriteLine($"Linux offset: {linuxOffset}");
Console.WriteLine($"Android offset: {androidOffset}");
Console.WriteLine("Windows UnityFS bundle: OK");
Console.WriteLine();
Console.WriteLine("PACKAGE VERIFICATION PASSED.");

// Build a part XML from the stock Strut definition.

static XDocument BuildPartXml(
    string stockXmlPath,
    string partId,
    string partName,
    string modifierName,
    string description,
    bool isStickyPad,
    bool isBallJoint = false)
{
    var partXml = XDocument.Load(stockXmlPath);

    var partRoot = partXml.Root
        ?? throw new Exception("Stock Strut XML has no root element.");

    partRoot.SetAttributeValue("name", partName);

    var partType = partRoot.Element("PartType")
        ?? throw new Exception("Stock Strut PartType not found.");

    partType.SetAttributeValue("id", partId);
    partType.SetAttributeValue("name", partName);
    partType.SetAttributeValue("prefabPath", prefabPath);

    if (isBallJoint)
    {
        var attachPoint = partType
            .Element("AttachPoints")?
            .Elements("AttachPoint")
            .ElementAt(2)
            ?? throw new Exception("Ball Joint Top attach point not found.");

        attachPoint.SetAttributeValue("jointType", "Hinge");
        attachPoint.SetAttributeValue("localJointAxis", "1,0,0");
        attachPoint.SetAttributeValue("requiresPhysicsJoint", "true");
    }
    else if (!isStickyPad)
    {
        // Keep the surface entry and bottom Base; remove the top Tip.

        var attachPoints = partType.Element("AttachPoints")
            ?? throw new Exception("Grapple AttachPoints element not found.");

        var points = attachPoints.Elements("AttachPoint").ToList();

        if (points.Count < 3)
            throw new Exception("Stock Strut Top/Tip attachment point not found.");

        points[2].Remove();
    }

    var modifiers = partType.Element("Modifiers")
        ?? throw new Exception("Stock Strut Modifiers element not found.");

    var config = modifiers.Element("Config")
        ?? throw new Exception("Part Config element not found.");

    if (!isStickyPad && !isBallJoint)
    {
        // Disable the Strut Fuselage modifier's editing tools on the Grapple.
        // Keep the existing Body1 mesh type unchanged for this test.

        var fuselageModifier = modifiers.Element("Fuselage")
            ?? throw new Exception("Grapple Fuselage modifier not found.");

        fuselageModifier.SetAttributeValue("autoResize", "false");
        fuselageModifier.SetAttributeValue("toolAutoAdaptBottom", "false");
        fuselageModifier.SetAttributeValue("toolAutoAdaptTop", "false");
        fuselageModifier.SetAttributeValue("toolIgnore", "false");
        fuselageModifier.SetAttributeValue("toolResizeBottom", "false");
        fuselageModifier.SetAttributeValue("toolResizeHeight", "false");
        fuselageModifier.SetAttributeValue("toolResizeRadius", "false");
        fuselageModifier.SetAttributeValue("toolResizeTop", "false");
        fuselageModifier.SetAttributeValue("toolShapeBottom", "false");
        fuselageModifier.SetAttributeValue("toolShapeTop", "false");
        fuselageModifier.SetAttributeValue("toolSupportsAddSection", "false");
        fuselageModifier.SetAttributeValue("inspectorEnabled", "false");
        fuselageModifier.SetAttributeValue("partPropertiesEnabled", "false");
    }

    config.SetAttributeValue("supportsActivation", "true");
    config.SetAttributeValue(
        "autoActivateIfNoStageOrActivationGroup",
        "false");

    if (isStickyPad)
    {
        modifiers.Add(
            new XElement(
                modifierName,
                new XAttribute("Enabled", "true"),
                new XAttribute("StickyMode", "true"),
                new XAttribute("Strength", "5000"),
                new XAttribute("GripRange", "1")));
    }
    else if (isBallJoint)
    {
        modifiers.Add(
            new XElement(
                modifierName,
                new XAttribute("AttachPointIndex", "2"),
                new XAttribute("LeftRightRange", "45"),
                new XAttribute("ForwardBackwardRange", "45"),
                new XAttribute("MaxSpeed", "90"),
                new XAttribute("Strength", "400000")));
    }
    else
    {
        modifiers.Add(
            new XElement(
                modifierName,
                new XAttribute("MaxDistance", "750")));
    }

    // Sticky Pad and Ball Joint retain their controllers.
    // Grapple intentionally has no InputController.

    if (isBallJoint)
    {
        modifiers.Add(CreateBallJointInputControllerDefinition(
            "BallJointLR", "Slider1"));

        modifiers.Add(CreateBallJointInputControllerDefinition(
            "BallJointFB", "Slider2"));
    }
    else if (isStickyPad)
    {
        modifiers.Add(CreateInputControllerDefinition());
    }

    var designerPart = partRoot
        .Element("DesignerParts")?
        .Elements("DesignerPart")
        .FirstOrDefault()
        ?? throw new Exception("Stock Strut DesignerPart not found.");

    designerPart.SetAttributeValue("name", partName);
    designerPart.SetAttributeValue("category", "Structural");
    designerPart.SetAttributeValue("description", description);

    var assemblyPart = designerPart
        .Descendants("Part")
        .FirstOrDefault()
        ?? throw new Exception("Designer assembly Part not found.");

    assemblyPart.SetAttributeValue("id", "1");
    assemblyPart.SetAttributeValue("partType", partId);
    assemblyPart.SetAttributeValue("position", "0,0,0");
    assemblyPart.SetAttributeValue(
        "rotation",
        "-3.385878E-06,0,0");
    assemblyPart.SetAttributeValue("materials", "2,4,4,2,2");
    assemblyPart.SetAttributeValue("texture", "Default");

    assemblyPart.Add(new XElement(modifierName));

    // Match the modifier controller setup in the designer assembly.

    if (isBallJoint)
    {
        assemblyPart.Add(
            new XElement("InputController",
                new XAttribute("inputRange", "1")));

        assemblyPart.Add(
            new XElement("InputController",
                new XAttribute("inputRange", "1")));
    }
    else if (isStickyPad)
    {
        assemblyPart.Add(
            new XElement(
                "InputController",
                new XAttribute("inputRange", "1")));
    }

    if (isStickyPad)
    {
        var fuselage = assemblyPart.Element("Fuselage")
            ?? throw new Exception("Designer Fuselage element not found.");

        fuselage.SetAttributeValue("bottomScale", "0.25,0.25");
        fuselage.SetAttributeValue(
            "cornerRadiuses",
            "0,0,0,0,0,0,0,0");
        fuselage.SetAttributeValue("depthCurve", "0");
        fuselage.SetAttributeValue(
            "offset",
            "0,0.02499998,1.477367E-09");
        fuselage.SetAttributeValue("topScale", "0.25,0.25");
        fuselage.SetAttributeValue("volume", "0.0124999918");
        fuselage.SetAttributeValue("volumeInner", "0.00949999");
        fuselage.SetAttributeValue("version", "3");
    }

    return partXml;
}

static XElement CreateBallJointInputControllerDefinition(
    string inputId,
    string input)
{
    return new XElement(
        "InputController",
        new XAttribute("activationGroup", "0"),
        new XAttribute("currentValue", "0"),
        new XAttribute("ignorePartActivationState", "false"),
        new XAttribute("input", input),
        new XAttribute("invert", "false"),
        new XAttribute("invertOnMirror", "false"),
        new XAttribute("invertType", "Axis"),
        new XAttribute("max", "1"),
        new XAttribute("min", "-1"),
        new XAttribute("outputCurveOffset", "0"),
        new XAttribute("showActivationGroup", "false"),
        new XAttribute("showInputAxis", "true"),
        new XAttribute("showInvert", "true"),
        new XAttribute("type", "Standard"),
        new XAttribute("zeroOnDeactivate", "false"),
        new XAttribute("inputId", inputId),
        new XAttribute("inspectorEnabled", "true"),
        new XAttribute("partPropertiesEnabled", "true"),
        new XAttribute("staticPriceAndMass", "false"));
}

static XElement CreateInputControllerDefinition()
{
    return new XElement(
        "InputController",
        new XAttribute("activationGroup", "0"),
        new XAttribute("currentValue", "1"),
        new XAttribute("ignorePartActivationState", "false"),
        new XAttribute("input", "Slider1"),
        new XAttribute("invert", "false"),
        new XAttribute("invertOnMirror", "false"),
        new XAttribute("invertType", "Axis"),
        new XAttribute("max", "1"),
        new XAttribute("min", "0"),
        new XAttribute("outputCurveOffset", "0"),
        new XAttribute("showActivationGroup", "false"),
        new XAttribute("showInputAxis", "true"),
        new XAttribute("showInvert", "true"),
        new XAttribute("type", "Standard"),
        new XAttribute("zeroOnDeactivate", "true"),
        new XAttribute("inputId", "PowerInput"),
        new XAttribute("inspectorEnabled", "true"),
        new XAttribute("partPropertiesEnabled", "true"),
        new XAttribute("staticPriceAndMass", "false"));
}

// Add a part XML as a new Unity TextAsset and register it
// in the AssetBundle preload table and container map.

static AssetFileInfo AddPartTextAsset(
    AssetsManager manager,
    AssetsFileInstance assets,
    AssetTypeValueField templateField,
    AssetTypeValueField preloadArray,
    AssetTypeValueField containerArray,
    string xmlText,
    string assetName,
    string bundlePath,
    long pathId)
{
    var newPartField = templateField.Clone();

    newPartField["m_Name"].AsString = assetName;
    newPartField["m_Script"].AsByteArray =
        Encoding.UTF8.GetBytes(xmlText);

    var newPartInfo = AssetFileInfo.Create(
        assets.file,
        pathId,
        49,
        manager.ClassDatabase!,
        false);

    newPartInfo.SetNewData(newPartField);

    assets.file.Metadata.AddAssetInfo(newPartInfo);

    int newPreloadIndex = preloadArray.Children.Count;

    var newPreloadEntry =
        preloadArray.Children.Last().Clone();

    newPreloadEntry["m_FileID"].AsInt = 0;
    newPreloadEntry["m_PathID"].AsLong = pathId;

    preloadArray.Children.Add(newPreloadEntry);
    preloadArray.AsArray =
        new AssetTypeArrayInfo(preloadArray.Children.Count);

    var newContainerEntry =
        containerArray.Children.Last().Clone();

    newContainerEntry["first"].AsString = bundlePath;

    var containerData = newContainerEntry["second"];

    containerData["preloadIndex"].AsInt = newPreloadIndex;
    containerData["preloadSize"].AsInt = 1;
    containerData["asset"]["m_FileID"].AsInt = 0;
    containerData["asset"]["m_PathID"].AsLong = pathId;

    containerArray.Children.Add(newContainerEntry);
    containerArray.AsArray =
        new AssetTypeArrayInfo(containerArray.Children.Count);

    Console.WriteLine(
        $"{assetName} added. PathID: {pathId}, preload index: {newPreloadIndex}");

    return newPartInfo;
}

// Verify a part's XML, modifier, preload entry, and container entry.

static void VerifyPartAsset(
    AssetsManager manager,
    AssetsFileInstance assets,
    AssetFileInfo partInfo,
    string assetName,
    string partId,
    string modifierName,
    string bundlePath,
    bool requiresInputController,
    AssetTypeValueField preloadArray,
    AssetTypeValueField containerArray)
{
    var field = manager.GetBaseField(
        assets, partInfo, AssetReadFlags.None);

    if (field["m_Name"].AsString != assetName)
        throw new Exception($"{assetName} name verification failed.");

    string xmlText = Encoding.UTF8.GetString(
        field["m_Script"].AsByteArray);

    var document = XDocument.Parse(xmlText);

    if (document.Root?
            .Element("PartType")?
            .Attribute("id")?.Value != partId)
    {
        throw new Exception($"{assetName} part ID verification failed.");
    }

    var modifiers = document.Root?
        .Element("PartType")?
        .Element("Modifiers")
        ?? throw new Exception($"{assetName} Modifiers element is missing.");

    if (modifiers.Element(modifierName) == null)
        throw new Exception($"{assetName} modifier defaults are missing.");

    if (modifiers.Element("Config")?
            .Attribute("supportsActivation")?.Value != "true")
    {
        throw new Exception($"{assetName} activation support is missing.");
    }

    bool hasModifierInputController =
        modifiers.Element("InputController") != null;

    if (requiresInputController && !hasModifierInputController)
    {
        throw new Exception($"{assetName} InputController is missing.");
    }

    if (!requiresInputController && hasModifierInputController)
    {
        throw new Exception($"{assetName} should not have an InputController.");
    }

    var assemblyPart = document.Root?
        .Element("DesignerParts")?
        .Descendants("Part")
        .FirstOrDefault()
        ?? throw new Exception($"{assetName} designer assembly Part is missing.");

    if (assemblyPart.Element(modifierName) == null)
        throw new Exception($"{assetName} assembly modifier is missing.");

    bool hasDesignerInputController =
        assemblyPart.Element("InputController") != null;

    if (requiresInputController && !hasDesignerInputController)
    {
        throw new Exception(
            $"{assetName} designer InputController is missing.");
    }

    if (!requiresInputController && hasDesignerInputController)
    {
        throw new Exception(
            $"{assetName} designer should not have an InputController.");
    }

    bool verifiedPreload = preloadArray.Children.Any(
        x => x["m_PathID"].AsLong == partInfo.PathId);

    bool verifiedContainer = containerArray.Children.Any(
        x =>
            x["first"].AsString == bundlePath &&
            x["second"]["asset"]["m_PathID"].AsLong == partInfo.PathId);

    if (!verifiedPreload)
        throw new Exception($"{assetName} preload registration failed.");

    if (!verifiedContainer)
        throw new Exception($"{assetName} container registration failed.");

    Console.WriteLine($"{assetName}: XML and bundle checks passed.");
}

// Recursively locate an array node whose children match
// the requested Unity array element structure.

static AssetTypeValueField? FindArrayNode(
    AssetTypeValueField field,
    Func<AssetTypeValueField, bool> elementMatcher)
{
    AssetTypeArrayInfo? arrayInfo = null;

    try
    {
        arrayInfo = field.AsArray;
    }
    catch
    {
        // This field is not an array.
    }

    if (arrayInfo != null &&
        field.Children.Any(elementMatcher))
    {
        return field;
    }

    foreach (var child in field.Children)
    {
        var result = FindArrayNode(child, elementMatcher);

        if (result != null)
            return result;
    }

    return null;
}

static bool HasChild(
    AssetTypeValueField field,
    string childName)
{
    return field.Children.Any(
        child => child.FieldName == childName);
}
