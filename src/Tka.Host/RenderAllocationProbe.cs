using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Tka.Compatibility;

internal static class RenderAllocationProbe
{
    public static object Run(GraphicsDevice device, SpriteBatch batch, RenderTarget2D stage)
    {
        const int iterations = 10000;
        var results = new List<object>();
        using var unrelated = new RenderTarget2D(device, 16, 16);
        foreach (var name in new[] { "canvas", "stage", "unrelated", "backbuffer" })
        {
            PcDisplay.BeginFrame();
            if (name != "canvas") device.SetRenderTarget(name == "stage" ? stage : name == "unrelated" ? unrelated : null);
            var original = Matrix.CreateTranslation(3, 7, 0);
            var expected = name is "canvas" or "stage" ? original * Matrix.CreateScale(PcDisplay.Current.InternalScale, PcDisplay.Current.InternalScale, 1) : original;
            for (int i = 0; i < 100; i++) _ = PcDisplay.CanvasTransform(batch, original);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < iterations; i++)
                if (PcDisplay.CanvasTransform(batch, original) != expected) throw new InvalidDataException("Render transform changed.");
            var optimized = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < iterations; i++) GC.KeepAlive(device.GetRenderTargets());
            var previousLookup = GC.GetAllocatedBytesForCurrentThread() - before;
            if (optimized != 0) throw new InvalidDataException("Render bridge allocated after warmup.");
            results.Add(new { target = name, iterations, old_target_lookup_bytes = previousLookup, optimized_bridge_bytes = optimized, transform_preserved = true });
        }
        device.SetRenderTarget(null);
        return results;
    }
}
