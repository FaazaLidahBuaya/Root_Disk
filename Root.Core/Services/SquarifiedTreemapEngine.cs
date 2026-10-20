using System;
using System.Collections.Generic;
using System.Linq;
using Root.Core.Models;
using Root.Core.Utilities;

namespace Root.Core.Services;

/// <summary>
/// Implementasi algoritma Squarified Treemap (Bruls, Huizing, van Wijk).
/// Menghasilkan tata letak kotak dengan rasio aspek mendekati 1 (persegi)
/// di mana luas setiap kotak sebanding dengan ukuran bita item terhadap item lain pada tingkat yang sama.
/// </summary>
public class SquarifiedTreemapEngine : ITreemapEngine
{
    private const double MinZeroByteAreaFraction = 0.005; // 0.5% dari area kanvas untuk item 0 bita
    private const string AggregateMarker = "::<ROOT-TREEMAP-AGGREGATE>";

    public IReadOnlyList<TreemapNode> CalculateLayout(IReadOnlyList<FileSystemItemModel> items, double containerWidth, double containerHeight)
    {
        if (items == null || items.Count == 0 || containerWidth <= 0 || containerHeight <= 0)
        {
            return Array.Empty<TreemapNode>();
        }

        double totalArea = containerWidth * containerHeight;
        double totalBytes = 0;
        int zeroCount = 0;

        foreach (var item in items)
        {
            if (item.SizeBytes > 0)
            {
                totalBytes += item.SizeBytes;
            }
            else
            {
                zeroCount++;
            }
        }

        // Tentukan alokasi area target untuk setiap item
        var itemAreas = new List<(FileSystemItemModel Item, double Area, double Percentage)>(items.Count);

        if (totalBytes <= 0)
        {
            // Semua item berukuran 0 bita: bagi area secara merata
            double equalArea = totalArea / items.Count;
            double equalPct = 100.0 / items.Count;
            foreach (var it in items)
            {
                itemAreas.Add((it, equalArea, equalPct));
            }
        }
        else
        {
            // Ada item dengan ukuran > 0
            double reservedForZero = 0;
            if (zeroCount > 0)
            {
                // Alokasikan area kecil untuk item 0 bita agar tetap terlihat dan dapat diklik
                double maxZeroFraction = Math.Min(0.05, MinZeroByteAreaFraction * zeroCount);
                reservedForZero = totalArea * maxZeroFraction;
            }

            double effectiveAreaForNonZero = totalArea - reservedForZero;
            double zeroAreaEach = zeroCount > 0 ? reservedForZero / zeroCount : 0;

            foreach (var it in items)
            {
                if (it.SizeBytes > 0)
                {
                    double area = ((double)it.SizeBytes / totalBytes) * effectiveAreaForNonZero;
                    double pct = ((double)it.SizeBytes / totalBytes) * 100.0;
                    itemAreas.Add((it, area, pct));
                }
                else
                {
                    itemAreas.Add((it, zeroAreaEach, 0.0));
                }
            }
        }

        // Urutkan item berdasarkan area target secara menurun (krusial untuk Squarified Treemap)
        var sorted = itemAreas.OrderByDescending(x => x.Area).ToList();

        var result = new List<TreemapNode>(items.Count);
        double rx = 0;
        double ry = 0;
        double rw = containerWidth;
        double rh = containerHeight;

        var currentRow = new List<(FileSystemItemModel Item, double Area, double Percentage)>();
        double currentRowAreaSum = 0;

        for (int i = 0; i < sorted.Count; i++)
        {
            var candidate = sorted[i];
            double shorterSide = Math.Min(rw, rh);

            if (currentRow.Count == 0)
            {
                currentRow.Add(candidate);
                currentRowAreaSum = candidate.Area;
                continue;
            }

            double currentWorst = CalculateWorstAspectRatio(currentRow, currentRowAreaSum, shorterSide);
            double newAreaSum = currentRowAreaSum + candidate.Area;
            double newWorst = CalculateWorstAspectRatioWithNew(currentRow, candidate.Area, newAreaSum, shorterSide);

            if (newWorst <= currentWorst)
            {
                // Menambahkan item meningkatkan/mempertahankan rasio aspek
                currentRow.Add(candidate);
                currentRowAreaSum = newAreaSum;
            }
            else
            {
                // Finalisasi baris saat ini
                LayoutRow(currentRow, currentRowAreaSum, ref rx, ref ry, ref rw, ref rh, containerWidth, containerHeight, result);

                // Mulai baris baru dengan kandidat saat ini
                currentRow.Clear();
                currentRow.Add(candidate);
                currentRowAreaSum = candidate.Area;
            }
        }

        // Finalisasi baris terakhir yang tersisa
        if (currentRow.Count > 0)
        {
            LayoutRow(currentRow, currentRowAreaSum, ref rx, ref ry, ref rw, ref rh, containerWidth, containerHeight, result);
        }

        return result;
    }

    private static TreemapNode CreateNode(FileSystemItemModel item) => item.FullPath.EndsWith(AggregateMarker, StringComparison.Ordinal)
        ? new TreemapNode(item, isAggregate: true, aggregatedItemCount: item.DirectoryCount)
        : new TreemapNode(item);

    private static double CalculateWorstAspectRatio(List<(FileSystemItemModel Item, double Area, double Percentage)> row, double sumArea, double length)
    {
        if (sumArea <= 0 || length <= 0) return double.MaxValue;
        double lengthSq = length * length;
        double sumAreaSq = sumArea * sumArea;

        double worst = 0;
        foreach (var item in row)
        {
            double a = item.Area;
            if (a <= 0) continue;
            double r1 = (lengthSq * a) / sumAreaSq;
            double r2 = sumAreaSq / (lengthSq * a);
            double ratio = Math.Max(r1, r2);
            if (ratio > worst) worst = ratio;
        }

        return worst > 0 ? worst : double.MaxValue;
    }

    private static double CalculateWorstAspectRatioWithNew(List<(FileSystemItemModel Item, double Area, double Percentage)> row, double newArea, double sumArea, double length)
    {
        if (sumArea <= 0 || length <= 0) return double.MaxValue;
        double lengthSq = length * length;
        double sumAreaSq = sumArea * sumArea;

        double worst = 0;
        foreach (var item in row)
        {
            double a = item.Area;
            if (a <= 0) continue;
            double r1 = (lengthSq * a) / sumAreaSq;
            double r2 = sumAreaSq / (lengthSq * a);
            double ratio = Math.Max(r1, r2);
            if (ratio > worst) worst = ratio;
        }

        if (newArea > 0)
        {
            double r1 = (lengthSq * newArea) / sumAreaSq;
            double r2 = sumAreaSq / (lengthSq * newArea);
            double ratio = Math.Max(r1, r2);
            if (ratio > worst) worst = ratio;
        }

        return worst > 0 ? worst : double.MaxValue;
    }

    private static void LayoutRow(
        List<(FileSystemItemModel Item, double Area, double Percentage)> row,
        double sumArea,
        ref double rx,
        ref double ry,
        ref double rw,
        ref double rh,
        double containerW,
        double containerH,
        List<TreemapNode> result)
    {
        if (row.Count == 0 || rw <= 0 || rh <= 0) return;

        bool isHorizontal = rw <= rh; // Jika lebar <= tinggi, potong horizontal (baris melintang penuh sepanjang rw)

        if (isHorizontal)
        {
            double rowHeight = sumArea / rw;
            // Cegah rowHeight melampaui sisa tinggi
            if (rowHeight > rh) rowHeight = rh;

            double currentX = rx;
            for (int i = 0; i < row.Count; i++)
            {
                var entry = row[i];
                double itemW = rowHeight > 0 ? entry.Area / rowHeight : 0;

                // Untuk item terakhir pada baris, rapikan ke batas kanan
                if (i == row.Count - 1)
                {
                    itemW = (rx + rw) - currentX;
                }

                itemW = Math.Max(0, Math.Min(itemW, containerW - currentX));
                double finalH = Math.Max(0, Math.Min(rowHeight, containerH - ry));

                var node = CreateNode(entry.Item);
                node.X = currentX;
                node.Y = ry;
                node.Width = itemW;
                node.Height = finalH;
                node.PercentageOfParent = entry.Percentage;
                node.ColorHex = TreemapColorHelper.GetColorForExtension(entry.Item.Extension, entry.Item.IsDirectory);

                result.Add(node);
                currentX += itemW;
            }

            ry += rowHeight;
            rh -= rowHeight;
        }
        else
        {
            // Potong vertikal (kolom menurun penuh sepanjang rh)
            double colWidth = sumArea / rh;
            if (colWidth > rw) colWidth = rw;

            double currentY = ry;
            for (int i = 0; i < row.Count; i++)
            {
                var entry = row[i];
                double itemH = colWidth > 0 ? entry.Area / colWidth : 0;

                // Untuk item terakhir pada kolom, rapikan ke batas bawah
                if (i == row.Count - 1)
                {
                    itemH = (ry + rh) - currentY;
                }

                itemH = Math.Max(0, Math.Min(itemH, containerH - currentY));
                double finalW = Math.Max(0, Math.Min(colWidth, containerW - rx));

                var node = CreateNode(entry.Item);
                node.X = rx;
                node.Y = currentY;
                node.Width = finalW;
                node.Height = itemH;
                node.PercentageOfParent = entry.Percentage;
                node.ColorHex = TreemapColorHelper.GetColorForExtension(entry.Item.Extension, entry.Item.IsDirectory);

                result.Add(node);
                currentY += itemH;
            }

            rx += colWidth;
            rw -= colWidth;
        }
    }
}
