using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

public sealed class QuizHeaderModel {
    // Coordinates in the 762 x 586 reference layout. The timer digits lie between these ROIs.
    private static readonly Rectangle Timer = new Rectangle(591, 25, 39, 44);
    private static readonly Rectangle Digit = new Rectangle(708, 38, 15, 18);
    private static readonly Rectangle Suffix = new Rectangle(723, 38, 27, 18);
    private readonly List<Sample> examples = new List<Sample>();
    private double timerThreshold = 0.56;

    private sealed class Sample {
        public int Number;
        public bool[,] Icon, Numeral, Ending;
    }

    public static QuizHeaderModel Train(string folder) {
        var model = new QuizHeaderModel();
        for (int number = 1; number <= 3; number++) {
            foreach (string file in GetSamples(folder, "quiz-" + number)) {
                using (var bitmap = new Bitmap(file)) {
                    var icon = ReadMask(bitmap, Timer, 0);
                    var digit = ReadMask(bitmap, Digit, 1);
                    var suffix = ReadMask(bitmap, Suffix, 2);
                    if (Count(icon) < 70 || Count(digit) < 7 || Count(suffix) < 18) continue;
                    model.examples.Add(new Sample { Number = number, Icon = icon, Numeral = digit, Ending = suffix });
                }
            }
            if (!model.examples.Exists(sample => sample.Number == number))
                throw new InvalidOperationException("Missing readable quiz header: " + number);
        }
        string[] normals = GetSamples(folder, "normal");
        if (normals.Length == 0) throw new InvalidOperationException("Missing normal screen sample.");
        foreach (string file in normals) {
            using (var bitmap = new Bitmap(file)) {
                double score = model.TimerScore(ReadMask(bitmap, Timer, 0));
                if (score >= 0.82) throw new InvalidOperationException("A normal sample contains the quiz timer.");
                model.timerThreshold = Math.Max(model.timerThreshold, score + 0.1);
            }
        }
        return model;
    }

    private static string[] GetSamples(string folder, string label) {
        string path = Path.Combine(folder, label);
        return Directory.Exists(path) ? Directory.GetFiles(path, "*.png") : new string[0];
    }

    // 0 means no timer; -1 means the timer is visible but the round is uncertain.
    public int Read(Bitmap bitmap) {
        if (TimerScore(ReadMask(bitmap, Timer, 0)) < timerThreshold) return 0;
        var numeral = ReadMask(bitmap, Digit, 1);
        var suffix = ReadMask(bitmap, Suffix, 2);
        var scores = new double[4];
        foreach (Sample example in examples) {
            if (Similarity(example.Ending, suffix, 3) < 0.52) continue;
            scores[example.Number] = Math.Max(scores[example.Number], Similarity(example.Numeral, numeral, 4));
        }
        int best = 1;
        for (int number = 2; number <= 3; number++)
            if (scores[number] > scores[best]) best = number;
        if (scores[best] < 0.57) return -1;
        for (int number = 1; number <= 3; number++)
            if (number != best && scores[best] - scores[number] < 0.10) return -1;
        return best;
    }

    private double TimerScore(bool[,] mask) {
        double best = 0;
        foreach (Sample example in examples) best = Math.Max(best, Similarity(example.Icon, mask, 4));
        return best;
    }

    private static bool[,] ReadMask(Bitmap bitmap, Rectangle region, int feature) {
        var mask = new bool[region.Width, region.Height];
        for (int y = 0; y < region.Height; y++) {
            for (int x = 0; x < region.Width; x++) {
                int sx = (int)((region.X + x + 0.5) * bitmap.Width / 762);
                int sy = (int)((region.Y + y + 0.5) * bitmap.Height / 586);
                Color color = bitmap.GetPixel(sx, sy);
                if (feature == 0)
                    mask[x, y] = color.G >= 100 && color.G >= color.R + 20 && color.G >= color.B + 55;
                else
                    mask[x, y] = Math.Abs(color.R - color.G) <= 24 && Math.Abs(color.G - color.B) <= 24 &&
                        color.G < (feature == 1 ? 112 : 181);
            }
        }
        return mask;
    }

    private static int Count(bool[,] mask) {
        int count = 0;
        foreach (bool set in mask) if (set) count++;
        return count;
    }

    private static double Similarity(bool[,] expected, bool[,] current, int shift) {
        int width = expected.GetLength(0), height = expected.GetLength(1);
        int total = Count(expected) + Count(current);
        if (total == 0) return 0;
        int best = 0;
        for (int dy = -shift; dy <= shift; dy++) {
            for (int dx = -shift; dx <= shift; dx++) {
                int overlap = 0;
                for (int y = Math.Max(0, -dy); y < Math.Min(height, height - dy); y++)
                    for (int x = Math.Max(0, -dx); x < Math.Min(width, width - dx); x++)
                        if (expected[x, y] && current[x + dx, y + dy]) overlap++;
                best = Math.Max(best, overlap);
            }
        }
        return (double)best / (total - best);
    }
}
