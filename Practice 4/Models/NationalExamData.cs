namespace Practice_4.Models;

public class NationalExamData
{
    public uint Id { get; set; }
    public string Year { get; set; } = null!;
    public uint Mathematics { get; set; }
    public uint Literature { get; set; }
    public uint Physics { get; set; }
    public uint Biology { get; set; }
    public uint English { get; set; }
    public uint Chemistry { get; set; }
    public uint History { get; set; }
    public uint Geography { get; set; }
    public uint CivicEducation { get; set; }
}