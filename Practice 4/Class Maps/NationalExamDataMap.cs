using CsvHelper.Configuration;
using Practice_4.Models;

namespace Practice_4.Class_Maps;

public sealed class NationalExamDataMap : ClassMap<NationalExamData>
{
    public NationalExamDataMap()
    {
        Map(m => m.Id).Name("SBD");
        Map(m => m.Mathematics).Name("Toan").Default(-1);
        Map(m => m.English).Name("Ngoai ngu").Default(-1);
        Map(m => m.Biology).Name("Sinh").Default(-1);
        Map(m => m.Geography).Name("Dia ly").Default(-1);
        Map(m => m.Chemistry).Name("Hoa").Default(-1);
        Map(m => m.History).Name("Lich su").Default(-1);
        Map(m => m.Literature).Name("Van").Default(-1);
        Map(m => m.Physics).Name("Ly").Default(-1);
        Map(m => m.CivicEducation).Name("GDCD").Default(-1);
        Map(m => m.Year).Name("Year");
    }
}