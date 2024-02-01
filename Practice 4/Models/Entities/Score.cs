using System.ComponentModel.DataAnnotations.Schema;

namespace Practice_4.Models.Entities;

[Table("Scores")]
public class Score : BaseEntity
{
    public uint? StudentId { get; set; }

    public Student? Student { get; set; }
    
    [ForeignKey("Subject")]
    public uint? SubjectId { get; set; }

    public Subject? Subject { get; set; }
    
    public float SubjectScore { get; set; }
}