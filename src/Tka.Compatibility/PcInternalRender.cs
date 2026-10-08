using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Tka.Compatibility;

// Bridges only the hash-verified Game1 stage allocation and two Camera.Draw calls.
public static class PcInternalRender
{
    private static RenderTarget2D? stage;

    public static RenderTarget2D CreateStageTarget(GraphicsDevice device, int width, int height,
        bool mipMap, SurfaceFormat format, DepthFormat depth)
    {
        if (width != 1280 || height != 720 || mipMap || format != SurfaceFormat.Color || depth != DepthFormat.None)
            throw new InvalidDataException("Unexpected original stage render-target signature.");
        Release();
        var scale = PcDisplay.Current.InternalScale;
        stage = new RenderTarget2D(device, width * scale, height * scale, mipMap, format, depth);
        LocalServices.Log($"Internal stage created: {stage.Width}x{stage.Height}; scale {scale}x; logical 1280x720.");
        return stage;
    }

    internal static Matrix? Transform(Texture target, Matrix? original) =>
        stage != null && ReferenceEquals(target, stage)
            ? (original ?? Matrix.Identity) * Matrix.CreateScale(stage.Width / 1280f, stage.Height / 720f, 1)
            : original;

    private static float StageScale(Texture2D texture)
    {
        if (stage is null || !ReferenceEquals(stage, texture)) throw new InvalidDataException("Unexpected camera stage texture.");
        return stage.Width / 1280f;
    }

    public static void DrawCameraTransform(SpriteBatch batch, Texture2D texture, Vector2 position,
        Rectangle? source, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float depth)
    {
        if (source != null) throw new InvalidDataException("Unexpected camera source rectangle.");
        var factor = StageScale(texture);
        // Origin is in source texels; position and apparent size stay in logical
        // game units. Preserve rotation, shake, flips, tint and camera zoom.
        batch.Draw(texture, position, null, color, rotation, origin * factor, scale / factor, effects, depth);
    }

    public static void DrawCameraEffect(SpriteBatch batch, Texture2D texture, Vector2 position, Color color)
    {
        // The active original pixel shader still samples the complete 0..1 UV
        // rectangle. Compensate for source size before the canvas transform.
        batch.Draw(texture, position, null, color, 0, Vector2.Zero, 1 / StageScale(texture), SpriteEffects.None, 0);
    }

    internal static void Release() { stage?.Dispose(); stage = null; }
}
