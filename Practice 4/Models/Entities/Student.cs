using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Practice_4.Models.Entities;

[Table("Students")]
public class Student : BaseEntity
{
    public Student()
    {
        Scores = new List<Score>();
    }

    public uint Code { get; set; }
    
    [ForeignKey("SchoolYear")]
    public uint? SchoolYearId { get; set; }

    public SchoolYear? SchoolYear { get; set; }

    public ICollection<Score>? Scores { get; set; }
}