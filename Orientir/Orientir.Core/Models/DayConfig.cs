using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orientir.Core.Models;

public class DayConfig
{
    [Key]
    public int Id { get; set; }

    public int Day { get; set; } = 1;        // номер дня (1..n)
    public string Folder { get; set; } = ""; // підтека дня: "D_1"
    public string Label { get; set; } = "";  // підпис: "30 травня"

    // Зв'язок зі змаганням (FK).
    public int EventConfigId { get; set; }
    [ForeignKey(nameof(EventConfigId))]
    public EventConfig? Event { get; set; }
}
