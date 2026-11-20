using Microsoft.Xna.Framework.Content;
using XnaContentCompiler.Core.Content.Xnb;

namespace XnaContentCompiler.Core.Tests;

public sealed class XnaTypeReadersTests
{
    [Fact]
    public void MapsBuiltInReadersToXnaAssemblies()
    {
        Assert.True(XnaTypeReaders.TryMap(
            "Microsoft.Xna.Framework.Content.Texture2DReader", out var texture));
        Assert.Equal(XnaTypeReaders.Texture2DReader, texture);

        Assert.True(XnaTypeReaders.TryMap(
            "Microsoft.Xna.Framework.Content.SpriteFontReader, MonoGame.Framework, Version=3.8.6.1, Culture=neutral, PublicKeyToken=null",
            out var font));
        Assert.Equal(XnaTypeReaders.SpriteFontReader, font);

        Assert.True(XnaTypeReaders.TryMap(
            "Microsoft.Xna.Framework.Content.SoundEffectReader", out var sound));
        Assert.Equal(XnaTypeReaders.SoundEffectReader, sound);
    }

    [Fact]
    public void MapsGenericListReadersAndElementTypes()
    {
        Assert.True(XnaTypeReaders.TryMap(
            "Microsoft.Xna.Framework.Content.ListReader`1[[Microsoft.Xna.Framework.Rectangle, MonoGame.Framework, Version=3.8.6.1, Culture=neutral, PublicKeyToken=null]]",
            out var rectangle));

        Assert.Contains("Microsoft.Xna.Framework.Rectangle, " + XnaTypeReaders.CoreAssembly, rectangle);
        Assert.EndsWith("]], " + XnaTypeReaders.CoreAssembly, rectangle);

        Assert.True(XnaTypeReaders.TryMap(
            "Microsoft.Xna.Framework.Content.ListReader`1[[System.Char, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]]",
            out var chars));

        Assert.Contains("System.Char, " + XnaTypeReaders.MscorlibAssembly, chars);
    }

    [Fact]
    public void LeavesUnknownReadersUntouched()
    {
        const string custom = "MyGame.Content.CustomReader, MyGame.Content, Version=1.0.0.0";
        Assert.False(XnaTypeReaders.TryMap(custom, out var mapped));
        Assert.Equal(custom, mapped);
    }

    [Fact]
    public void DoesNotMapReadersThisCompilerNeverEmits()
    {
        // Effect content is not supported; mapping it to the core assembly would produce
        // an XNB that XNA cannot load, so it must be left untouched and reported.
        const string effect = "Microsoft.Xna.Framework.Content.EffectReader";
        Assert.False(XnaTypeReaders.TryMap(effect, out var mapped));
        Assert.Equal(effect, mapped);

        const string array = "Microsoft.Xna.Framework.Content.ArrayReader`1[[System.Int32, mscorlib]]";
        Assert.False(XnaTypeReaders.TryMap(array, out _));
    }

    [Fact]
    public void XnaReaderNamesResolveInMonoGame()
    {
        var names = new[]
        {
            XnaTypeReaders.Texture2DReader,
            XnaTypeReaders.SpriteFontReader,
            XnaTypeReaders.SoundEffectReader,
            "Microsoft.Xna.Framework.Content.ListReader`1[[Microsoft.Xna.Framework.Rectangle, " +
                XnaTypeReaders.CoreAssembly + "]], " + XnaTypeReaders.CoreAssembly,
            "Microsoft.Xna.Framework.Content.ListReader`1[[System.Char, " +
                XnaTypeReaders.MscorlibAssembly + "]], " + XnaTypeReaders.CoreAssembly,
        };

        foreach (var name in names)
        {
            var prepared = ContentTypeReaderManager.PrepareType(name);
            Assert.NotNull(Type.GetType(prepared));
        }
    }
}
