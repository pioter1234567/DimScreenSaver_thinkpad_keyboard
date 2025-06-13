using System;
using System.Drawing;
using System.Windows.Forms;

namespace DimScreenSaver
{
    public class BrightnessLevelEditor : UserControl
    {
        private readonly Panel[] blocks = new Panel[10];
        private readonly int[] levels = new int[10]; // 0 = off, 1 = dim, 2 = full
        private ToolTip tip = new ToolTip();
        public event EventHandler LevelClicked;
        private readonly Image[] icons;
        private PictureBox brightnessBar;

        public BrightnessLevelEditor()
        {
            this.Size = new Size(300, 46); // 16 (pasek) + 30 (bloki)
            this.BackColor = Color.Transparent;

            icons = new Image[]
            {
            LoadEmbeddedImage("level0.png"),
            LoadEmbeddedImage("level1.png"),
            LoadEmbeddedImage("level2.png")
            };

            int barHeight = 16;

            brightnessBar = new PictureBox
            {
                Size = new Size(300, barHeight),
                Location = new Point(0, 0),
                Image = LoadEmbeddedImage("brightnessbar.png"),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            this.Controls.Add(brightnessBar);

            string raw = Properties.Settings.Default.KeyboardLevelMap;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var split = raw.Split(',');
                for (int i = 0; i < 10 && i < split.Length; i++)
                    levels[i] = int.TryParse(split[i], out var val) && val >= 0 && val <= 2 ? val : 0;
            }
            else
            {
                for (int i = 0; i < 10; i++)
                    levels[i] = 0;
            }

            for (int i = 0; i < 10; i++)
            {
                var block = new Panel
                {
                    Size = new Size(30, 30),
                    Location = new Point(i * 30, barHeight), // <--- poprawiony Y
                    Tag = i,
                    Cursor = Cursors.Hand,
                    BackgroundImageLayout = ImageLayout.Stretch
                };
                block.Click += Block_Click;
                this.Controls.Add(block);
                int rangeStart = i * 10;
                int rangeEnd = (i == 9) ? 100 : (i + 1) * 10 - 1;
                tip.SetToolTip(block, $"{rangeStart}%–{rangeEnd}%");

                blocks[i] = block;
                UpdateBlockVisual(i);
            }
        }


        private void Block_Click(object sender, EventArgs e)
        {
            var block = sender as Panel;
            int index = (int)block.Tag;

            levels[index] = (levels[index] + 1) % 3;
            UpdateBlockVisual(index);
                        
            LevelClicked?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateBlockVisual(int index)
        {
            blocks[index].BackgroundImage = icons[levels[index]];
        }

        public (int min, int max)[] GetRangesForLevel(int level)
        {
            var ranges = new System.Collections.Generic.List<(int, int)>();
            int start = -1;

            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] == level)
                {
                    if (start == -1) start = i;
                }
                else if (start != -1)
                {
                    ranges.Add((start * 10, (i * 10) - 1));
                    start = -1;
                }
            }

            if (start != -1)
            {
                ranges.Add((start * 10, 100));
            }

            return ranges.ToArray();
        }

        public int[] GetCurrentLevelMap()
        {
            return (int[])levels.Clone();
        }

        public void SetLevel(int index, int level)
        {
            if (index >= 0 && index < 10 && level >= 0 && level <= 2)
            {
                levels[index] = level;
                UpdateBlockVisual(index);
            }
        }



        private Image LoadEmbeddedImage(string name)
        {
            var asm = typeof(BrightnessLevelEditor).Assembly;
            var stream = asm.GetManifestResourceStream($"DimScreenSaver.Resources.{name}");
            return stream != null ? Image.FromStream(stream) : null;
        }


    }
}
