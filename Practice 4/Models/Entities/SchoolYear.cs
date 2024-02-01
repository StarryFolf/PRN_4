using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Practice_4.Models.Entities;

[Table("SchoolYears")]
public class SchoolYear : BaseEntity
{
    public SchoolYear()
    {
        Students = new List<Student>();
    }

    [MaxLength(4)]
    public string? Year { get; set; }

    public ICollection<Student>? Students { get; set; }
}