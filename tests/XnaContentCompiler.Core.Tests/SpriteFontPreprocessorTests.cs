using System.Xml.Linq;
using XnaContentCompiler.Core.Content.Fonts;

namespace XnaContentCompiler.Core.Tests;

public sealed class SpriteFontPreprocessorTests
{
    [Fact]
    public void NormalizesCharacterCodesAndResolvesFontFile()
    {
        var root = TestDirectory.Create();
        try
        {
            var source = Path.Combine(root, "default.spritefont");
            File.WriteAllText(source, """
<?xml version="1.0" encoding="utf-8"?>
<XnaContent xmlns:Graphics="Microsoft.Xna.Framework.Content.Pipeline.Graphics">
  <Asset Type="Graphics:FontDescription">
    <FontName>Arial</FontName>
    <Size>20</Size>
    <Spacing>0</Spacing>
    <UseKerning>true</UseKerning>
    <Style>Regular</Style>
    <DefaultCharacter>*</DefaultCharacter>
    <CharacterRegions>
      <CharacterRegion>
        <Start>&#32;</Start>
        <End>&#126;</End>
      </CharacterRegion>
    </CharacterRegions>
  </Asset>
</XnaContent>
""");

            var working = Path.Combine(root, "work");
            var prepared = SpriteFontPreprocessor.Prepare(source, working);

            Assert.True(File.Exists(prepared.SourcePath));

            var document = XDocument.Load(prepared.SourcePath, LoadOptions.PreserveWhitespace);
            Assert.Equal("32", Find(document, "Start").Value);
            Assert.Equal("126", Find(document, "End").Value);
            Assert.Equal("42", Find(document, "DefaultCharacter").Value);

            if (prepared.ResolvedFontPath != null)
            {
                Assert.True(File.Exists(prepared.ResolvedFontPath));
                var fontName = Find(document, "FontName").Value;
                Assert.True(File.Exists(Path.Combine(working, fontName)));
            }
            else
            {
                Assert.NotNull(prepared.Warning);
                Assert.Equal("Arial", Find(document, "FontName").Value);
            }

            Assert.Equal(
                " ",
                XDocument.Load(source, LoadOptions.PreserveWhitespace)
                    .Descendants()
                    .First(x => x.Name.LocalName == "Start")
                    .Value);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    private static XElement Find(XDocument document, string localName)
    {
        return document.Descendants().First(x => x.Name.LocalName == localName);
    }
}
