using LiveSplit.Model;
using System.Drawing;
using System.Threading;
using Image = System.Drawing.Image;

namespace LiveSplit.View;

public partial class MainWindow
{
    /// <summary>
    /// Offscreen rendering for --screenshot: updates the components, lets the size logic settle (it needs a few passes,
    /// like the first paints of the real window) and returns the final frame.
    /// </summary>
    public Image RenderSettledScreenShot(bool demo)
    {
        if (demo)
        {
            Model.Start();
            Thread.Sleep(1300);
            Model.Split();
            Thread.Sleep(700);
        }

        UpdateAllComponents();

        Image image = null;
        for (int pass = 0; pass < 8; pass++)
        {
            image?.Dispose();
            image = MakeScreenShot();
            UpdateAllComponents();
        }

        return image;
    }
}
