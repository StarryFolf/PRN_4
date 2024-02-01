using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Practice_4.Models.Entities;

[Table("Subjects")]
public class Subject : BaseEntity
{
    public Subject()
    {
        Scores = new List<Score>();
    }

    [MaxLength(50)]
    public string? Name { get; set; } = null!;

    public ICollection<Score>? Scores { get; set; }
}