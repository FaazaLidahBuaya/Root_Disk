using System;
using System.Collections.Generic;
using System.Linq;
using Root.Core.Models;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class TreemapAlgorithmTests
{
    private readonly SquarifiedTreemapEngine _engine = new();
    private const double Epsilon = 0.01;

    [Fact]
    public void CalculateLayout_ProportionalArea_CalculatesAreaProportionalToItemSize()
    {
        // 3 Berkas: 1000 B, 2000 B, 3000 B (Rasio 1 : 2 : 3)
        // Luas total kanvas = 600 x 400 = 240.000 px^2
        // Harapan luas:
        // Item 1 (1000): 1/6 * 240.000 = 40.000 px^2
        // Item 2 (2000): 2/6 * 240.000 = 80.000 px^2
        // Item 3 (3000): 3/6 * 240.000 = 120.000 px^2

        var items = new List<FileSystemItemModel>
        {
            new() { Name = "file1.dat", SizeBytes = 1000 },
            new() { Name = "file2.dat", SizeBytes = 2000 },
            new() { Name = "file3.dat", SizeBytes = 3000 }
        };

        double width = 600;
        double height = 400;
        double totalContainerArea = width * height;

        var nodes = _engine.CalculateLayout(items, width, height);

        Assert.Equal(3, nodes.Count);

        var node1 = nodes.First(n => n.Name == "file1.dat");
        var node2 = nodes.First(n => n.Name == "file2.dat");
        var node3 = nodes.First(n => n.Name == "file3.dat");

        // Periksa luas setiap node proporsional terhadap bita
        Assert.InRange(node1.Area, 40000 - 50, 40000 + 50);
        Assert.InRange(node2.Area, 80000 - 50, 80000 + 50);
        Assert.InRange(node3.Area, 120000 - 50, 120000 + 50);

        // Luas node 2 harus ~2x node 1, luas node 3 harus ~3x node 1
        Assert.InRange(node2.Area / node1.Area, 1.98, 2.02);
        Assert.InRange(node3.Area / node1.Area, 2.98, 3.02);

        // Total luas seluruh kotak harus memenuhi kanvas
        double sumArea = nodes.Sum(n => n.Area);
        Assert.InRange(sumArea, totalContainerArea - 1, totalContainerArea + 1);
    }

    [Fact]
    public void CalculateLayout_BoxBounds_AllNodesStayStrictlyInsideContainer()
    {
        var items = new List<FileSystemItemModel>
        {
            new() { Name = "A.iso", SizeBytes = 50000 },
            new() { Name = "B.zip", SizeBytes = 30000 },
            new() { Name = "C.mkv", SizeBytes = 15000 },
            new() { Name = "D.mp4", SizeBytes = 10000 },
            new() { Name = "E.docx", SizeBytes = 5000 },
            new() { Name = "F.txt", SizeBytes = 2000 },
            new() { Name = "G.log", SizeBytes = 1000 }
        };

        double width = 1024;
        double height = 768;

        var nodes = _engine.CalculateLayout(items, width, height);

        Assert.Equal(items.Count, nodes.Count);

        foreach (var node in nodes)
        {
            Assert.True(node.X >= 0, $"Node {node.Name} X ({node.X}) < 0");
            Assert.True(node.Y >= 0, $"Node {node.Name} Y ({node.Y}) < 0");
            Assert.True(node.Width > 0, $"Node {node.Name} Width ({node.Width}) <= 0");
            Assert.True(node.Height > 0, $"Node {node.Name} Height ({node.Height}) <= 0");
            Assert.True(node.X + node.Width <= width + Epsilon, $"Node {node.Name} melampaui lebar kanvas: {node.X + node.Width} > {width}");
            Assert.True(node.Y + node.Height <= height + Epsilon, $"Node {node.Name} melampaui tinggi kanvas: {node.Y + node.Height} > {height}");
        }
    }

    [Fact]
    public void CalculateLayout_NoOverlap_NoTwoBoxesIntersectEachOther()
    {
        var items = new List<FileSystemItemModel>
        {
            new() { Name = "f1", SizeBytes = 4500 },
            new() { Name = "f2", SizeBytes = 2500 },
            new() { Name = "f3", SizeBytes = 1800 },
            new() { Name = "f4", SizeBytes = 1200 },
            new() { Name = "f5", SizeBytes = 800 },
            new() { Name = "f6", SizeBytes = 400 },
            new() { Name = "f7", SizeBytes = 200 }
        };

        double width = 800;
        double height = 600;

        var nodes = _engine.CalculateLayout(items, width, height).ToList();

        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                var n1 = nodes[i];
                var n2 = nodes[j];

                double xOverlap = Math.Max(0, Math.Min(n1.X + n1.Width, n2.X + n2.Width) - Math.Max(n1.X, n2.X));
                double yOverlap = Math.Max(0, Math.Min(n1.Y + n1.Height, n2.Y + n2.Height) - Math.Max(n1.Y, n2.Y));

                double overlapArea = xOverlap * yOverlap;

                Assert.True(overlapArea < Epsilon, $"Tumpang tindih antara {n1.Name} dan {n2.Name}! Luas tumpang tindih = {overlapArea}");
            }
        }
    }

    [Fact]
    public void CalculateLayout_EmptyData_ReturnsEmptyListWithoutCrashing()
    {
        // 1. Daftar null
        var r1 = _engine.CalculateLayout(null!, 500, 500);
        Assert.Empty(r1);

        // 2. Daftar kosong
        var r2 = _engine.CalculateLayout(new List<FileSystemItemModel>(), 500, 500);
        Assert.Empty(r2);

        // 3. Dimensi kanvas 0 atau negatif
        var items = new List<FileSystemItemModel> { new() { Name = "test", SizeBytes = 100 } };
        var r3 = _engine.CalculateLayout(items, 0, 500);
        var r4 = _engine.CalculateLayout(items, 500, 0);
        var r5 = _engine.CalculateLayout(items, -100, 500);

        Assert.Empty(r3);
        Assert.Empty(r4);
        Assert.Empty(r5);
    }

    [Fact]
    public void CalculateLayout_SingleItem_OccupiesEntireContainer()
    {
        var items = new List<FileSystemItemModel>
        {
            new() { Name = "hanya_satu.bin", SizeBytes = 1048576 }
        };

        double width = 500;
        double height = 300;

        var nodes = _engine.CalculateLayout(items, width, height);

        Assert.Single(nodes);
        var node = nodes[0];
        Assert.Equal(0, node.X, 2);
        Assert.Equal(0, node.Y, 2);
        Assert.Equal(width, node.Width, 2);
        Assert.Equal(height, node.Height, 2);
        Assert.Equal(100.0, node.PercentageOfParent);
    }

    [Fact]
    public void CalculateLayout_ZeroByteItemsOnly_DistributesAreaEqually()
    {
        // 4 berkas kosong (0 bita)
        var items = new List<FileSystemItemModel>
        {
            new() { Name = "empty1.txt", SizeBytes = 0 },
            new() { Name = "empty2.txt", SizeBytes = 0 },
            new() { Name = "empty3.txt", SizeBytes = 0 },
            new() { Name = "empty4.txt", SizeBytes = 0 }
        };

        double width = 400;
        double height = 200;
        double totalArea = width * height;

        var nodes = _engine.CalculateLayout(items, width, height);

        Assert.Equal(4, nodes.Count);

        // Setiap kotak harus mendapat 1/4 area
        double expectedEach = totalArea / 4.0;
        foreach (var node in nodes)
        {
            Assert.True(node.Width > 0);
            Assert.True(node.Height > 0);
            Assert.InRange(node.Area, expectedEach - 1, expectedEach + 1);
            Assert.Equal(25.0, node.PercentageOfParent);
        }
    }

    [Fact]
    public void CalculateLayout_MixedZeroAndNonZeroBytes_AllocatesVisibleAreaForZeroBytes()
    {
        var items = new List<FileSystemItemModel>
        {
            new() { Name = "besar.iso", SizeBytes = 100000 },
            new() { Name = "sedang.zip", SizeBytes = 50000 },
            new() { Name = "kosong.txt", SizeBytes = 0 }
        };

        double width = 800;
        double height = 600;

        var nodes = _engine.CalculateLayout(items, width, height);

        Assert.Equal(3, nodes.Count);

        var emptyNode = nodes.First(n => n.Name == "kosong.txt");
        Assert.True(emptyNode.Width > 0, "Kotak 0 bita harus memiliki lebar > 0 agar dapat diklik.");
        Assert.True(emptyNode.Height > 0, "Kotak 0 bita harus memiliki tinggi > 0 agar dapat diklik.");
        Assert.True(emptyNode.Area > 0);

        var besarNode = nodes.First(n => n.Name == "besar.iso");
        var sedangNode = nodes.First(n => n.Name == "sedang.zip");
        // Rasio besar vs sedang harus tetap ~2 : 1
        Assert.InRange(besarNode.Area / sedangNode.Area, 1.95, 2.05);
    }

    [Fact]
    public void CalculateLayout_ExtremeDisparity_HandlesWithoutException()
    {
        // 1 Berkas sangat besar (10 GB) dan 1 berkas sangat kecil (100 B)
        var items = new List<FileSystemItemModel>
        {
            new() { Name = "sangat_besar.dat", SizeBytes = 10L * 1024L * 1024L * 1024L },
            new() { Name = "sangat_kecil.dat", SizeBytes = 100 }
        };

        var nodes = _engine.CalculateLayout(items, 1920, 1080);

        Assert.Equal(2, nodes.Count);
        foreach (var n in nodes)
        {
            Assert.True(n.Width > 0);
            Assert.True(n.Height > 0);
            Assert.True(n.X + n.Width <= 1920 + Epsilon);
            Assert.True(n.Y + n.Height <= 1080 + Epsilon);
        }
    }
}
