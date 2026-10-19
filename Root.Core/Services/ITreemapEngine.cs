using System.Collections.Generic;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Antarmuka mesin algoritma pembagian ruang bidang treemap.
/// </summary>
public interface ITreemapEngine
{
    /// <summary>
    /// Menghitung tata letak kotak-kotak treemap secara proporsional berdasarkan ukuran bita item sistem berkas.
    /// </summary>
    /// <param name="items">Daftar item (berkas dan direktori) pada tingkat yang sama.</param>
    /// <param name="containerWidth">Lebar bidang kanvas yang tersedia.</param>
    /// <param name="containerHeight">Tinggi bidang kanvas yang tersedia.</param>
    /// <returns>Daftar node treemap lengkap dengan koordinat dan dimensi geometris.</returns>
    IReadOnlyList<TreemapNode> CalculateLayout(IReadOnlyList<FileSystemItemModel> items, double containerWidth, double containerHeight);
}
