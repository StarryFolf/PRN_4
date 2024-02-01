namespace Practice_4.Models;

public class NationalExamData
{
    public uint Id { get; set; }
    public string Year { get; set; } = null!;
    public float Mathematics { get; set; }
    public float Literature { get; set; }
    public float Physics { get; set; }
    public float Biology { get; set; }
    public float English { get; set; }
    public float Chemistry { get; set; }
    public float History { get; set; }
    public float Geography { get; set; }
    public float CivicEducation { get; set; }
}