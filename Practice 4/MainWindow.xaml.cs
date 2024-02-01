using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Practice_4.Class_Maps;
using Practice_4.DB;
using Practice_4.Models;
using Practice_4.Models.Entities;
using Z.BulkOperations;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Practice_4
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private Valedictorian _valeWindow = new();
        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            grd_year_stats.Visibility = Visibility.Hidden;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            
        }

        private async void btn_Import_Click(object sender, RoutedEventArgs e)
        {
            var context = new AppDbContext();
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var path = dialog.FileName;
            if (path is null) return;
            using var reader = new StreamReader(path);
            using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csvReader.Context.RegisterClassMap<NationalExamDataMap>();
                var data = csvReader.GetRecords<NationalExamData>().ToList();

                var years = data.GroupBy(i => i.Year);

                try
                {
                    await InsertYearsIntoDatabase(context, years);
                    await InsertStudentsAndScoresIntoDatabase(context, data);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
                
            reader.Close();
        }

        private static async Task InsertYearsIntoDatabase(AppDbContext context, IEnumerable<IGrouping<string, NationalExamData>> years)
        {
            foreach (var year in years)
            {
                var existingYear = await context.SchoolYears.FirstOrDefaultAsync(i => i.Year == year.Key);
                if (existingYear is not null) continue;

                var yearToAdd = new SchoolYear
                {
                    Year = year.Key
                };
                
                await context.SchoolYears.AddAsync(yearToAdd);
                await context.SaveChangesAsync();
            }
        }

        private static async Task InsertStudentsAndScoresIntoDatabase(AppDbContext context, IEnumerable<NationalExamData> data)
        {
            var students = new ConcurrentBag<Student>();
            
            var options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = -1
            };
            
            var subjects = await context.Subjects.ToListAsync();

            uint studentIndex = 1;

            var index = 1;

            var semaphoreSlim = new SemaphoreSlim(1, 1);

            await Parallel.ForEachAsync(data, options, async (d, ct) =>
            {
                await using (var parallelContext = new AppDbContext())
                {
                    Interlocked.Increment(ref studentIndex);
                    Interlocked.Increment(ref index);
                    var scores = new ConcurrentBag<Score>();
                    var schoolYear = await parallelContext.SchoolYears.FirstOrDefaultAsync(x => x.Year == d.Year, ct);
                    var student = new Student
                    {
                        Id = studentIndex,
                        Code = d.Id,
                        SchoolYear = schoolYear,
                        SchoolYearId = schoolYear!.Id
                    };
                    Parallel.ForEach(subjects, subject =>
                    {
                        var score = new Score
                        {
                            Student = student,
                            StudentId = student.Id,
                            Subject = subject,
                            SubjectId = subject.Id,
                        };

                        var myType = typeof(NationalExamData);
                        var propInfo = myType.GetProperty(subject.Name!);
                        score.SubjectScore = (float)propInfo!.GetValue(d)!;
                        
                        scores.Add(score);
                    });
                    student.Scores = scores.ToList();
                    students.Add(student);
                    await semaphoreSlim.WaitAsync(ct);
                    try
                    {
                        if (index >= 100000)
                        {
                            Interlocked.Add(ref index, -index);
                            await parallelContext.BulkInsertOptimizedAsync(students, bulkOptions =>
                            {
                                bulkOptions.BatchSize = 110000;
                                bulkOptions.InsertIfNotExists = true;
                                bulkOptions.IncludeGraph = true;
                                bulkOptions.IncludeGraphOperationBuilder = operation =>
                                {
                                    if (operation is BulkOperation<Student>)
                                    {
                                        var bulk = (BulkOperation<Student>)operation;
                                        bulk.ColumnPrimaryKeyExpression = e => e.Id;
                                    }
                                    
                                    else if (operation is BulkOperation<Score>)
                                    {
                                        var bulk = (BulkOperation<Score>)operation;
                                        bulk.ColumnPrimaryKeyExpression = e => e.Id;
                                    }
                                };
                            }, cancellationToken: ct);
                            students.Clear();
                        }
                    }
                    finally
                    {
                        semaphoreSlim.Release();
                    }
                }
            });
            
            await context.BulkInsertOptimizedAsync(students, bulkOptions =>
            {
                bulkOptions.BatchSize = 110000;
                bulkOptions.InsertIfNotExists = true;
                bulkOptions.IncludeGraph = true;
            });
            students.Clear();
            
            await context.DisposeAsync();
        }

        private async void btn_Stat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var yearData = new List<ExamYearStats>();
                using (var context = new AppDbContext())
                {
                    foreach (var i in Enumerable.Range(2017, 5))
                    {
                        var yearItemCount = await context.Students
                            .Include(x => x.SchoolYear)
                            .Where(x => x.SchoolYear!.Year == i.ToString())
                            .CountAsync();

                        var subjects = await context.Subjects.ToListAsync();

                        var yearStat = new ExamYearStats
                        {
                            Year = i.ToString(),
                            StudentCount = (uint)yearItemCount
                        };

                        var baseQuery = context.Students.Include(x => x.Scores)!
                            .ThenInclude(x => x.Subject);

                        foreach (var subject in subjects)
                        {
                            var subjectStudentCount = await baseQuery
                                .Where(x => x.SchoolYear!.Year == i.ToString())
                                .Select(y => y.Scores
                                ).CountAsync(z => z!
                                                      .AsQueryable()
                                                      .FirstOrDefault(m =>
                                                          (m.Subject!.Name == subject.Name && m.SubjectScore >= 0)) !=
                                                  null);

                            var myType = typeof(ExamYearStats);
                            var propInfo = myType.GetProperty(subject.Name!);
                            propInfo!.SetValue(yearStat, (uint)subjectStudentCount, null);
                        }

                        yearData.Add(yearStat);
                    }
                }

                grd_year_stats.ItemsSource = yearData;

                grd_year_stats.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private async void btn_Vale_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    var a00 = context.Scores
                        .Include(x => x.Subject)
                        .Include(x => x.Student)
                        .ThenInclude(y => y!.SchoolYear)
                        .Where(x => x.Student!.SchoolYear!.Year == cmb_Years.Text
                                    && (x.Subject!.Name == "Mathematics" || x.Subject.Name == "Physics" ||
                                        x.Subject.Name == "Chemistry") && x.SubjectScore >= 0)
                        .Select(x => new
                        {
                            x.Student!.Code, x.SubjectScore, x.Subject!.Name
                        })
                        .GroupBy(x => x.Code)
                        .Select(x => new
                        {
                            x.Key,
                            Sum = x.Sum(y => y.SubjectScore)
                        });

                    var b00 = context.Scores
                        .Include(x => x.Subject)
                        .Include(x => x.Student)
                        .ThenInclude(y => y!.SchoolYear)
                        .Where(x => x.Student!.SchoolYear!.Year == cmb_Years.Text
                                    && (x.Subject!.Name == "Mathematics" || x.Subject.Name == "Chemistry" ||
                                        x.Subject.Name == "Biology") && x.SubjectScore >= 0)
                        .Select(x => new
                        {
                            x.Student!.Code, x.SubjectScore, x.Subject!.Name
                        })
                        .GroupBy(x => x.Code)
                        .Select(x => new
                        {
                            x.Key,
                            Sum = x.Sum(y => y.SubjectScore)
                        });

                    var c00 = context.Scores
                        .Include(x => x.Subject)
                        .Include(x => x.Student)
                        .ThenInclude(y => y!.SchoolYear)
                        .Where(x => x.Student!.SchoolYear!.Year == cmb_Years.Text
                                    && (x.Subject!.Name == "Literature" || x.Subject.Name == "History" ||
                                        x.Subject.Name == "Geography") && x.SubjectScore >= 0)
                        .Select(x => new
                        {
                            x.Student!.Code, x.SubjectScore, x.Subject!.Name
                        })
                        .GroupBy(x => x.Code)
                        .Select(x => new
                        {
                            x.Key,
                            Sum = x.Sum(y => y.SubjectScore)
                        });

                    var d01 = context.Scores
                        .Include(x => x.Subject)
                        .Include(x => x.Student)
                        .ThenInclude(y => y!.SchoolYear)
                        .Where(x => x.Student!.SchoolYear!.Year == cmb_Years.Text
                                    && (x.Subject!.Name == "Mathematics" || x.Subject.Name == "Chemistry" ||
                                        x.Subject.Name == "English") && x.SubjectScore >= 0)
                        .Select(x => new
                        {
                            x.Student!.Code, x.SubjectScore, x.Subject!.Name
                        })
                        .GroupBy(x => x.Code)
                        .Select(x => new
                        {
                            x.Key,
                            Sum = x.Sum(y => y.SubjectScore),
                        });

                    var a01 = context.Scores
                        .Include(x => x.Subject)
                        .Include(x => x.Student)
                        .ThenInclude(y => y!.SchoolYear)
                        .Where(x => x.Student!.SchoolYear!.Year == cmb_Years.Text
                                    && (x.Subject!.Name == "Mathematics" || x.Subject.Name == "Physics" ||
                                        x.Subject.Name == "English") && x.SubjectScore >= 0)
                        .Select(x => new
                        {
                            x.Student!.Code, x.SubjectScore, x.Subject!.Name
                        })
                        .GroupBy(x => x.Code)
                        .Select(x => new
                        {
                            x.Key,
                            Sum = x.Sum(y => y.SubjectScore)
                        });

                    var a00_Max = await a00.MaxAsync(x => x.Sum);
                    var b00_Max = await b00.MaxAsync(x => x.Sum);
                    var c00_Max = await c00.MaxAsync(x => x.Sum);
                    var a01_Max = await a01.MaxAsync(x => x.Sum);
                    var d01_Max = await d01.MaxAsync(x => x.Sum);

                    _valeWindow.lbl_Vale.Content = $"{cmb_Years.Text}'s Valedictory Scores of 5 Exam Groups";
                    _valeWindow.lbl_ValeStudent.Content = $"{cmb_Years.Text}'s Valedictorians' Info";
                    _valeWindow.grd_Vale.ItemsSource = new List<ValedictorianData>()
                    {
                        new()
                        {
                            A00 = a00_Max,
                            B00 = b00_Max,
                            C00 = c00_Max,
                            A01 = a01_Max,
                            D01 = d01_Max,
                        }
                    };

                    var a00StudentCode =
                        await a00.Where(x => x.Sum == a00_Max).Select(x => x.Key).FirstAsync()!;
                    var a00_subjects = await context.Scores
                        .Include(x => x.Subject)
                        .Include(x => x.Student)
                        .ThenInclude(y => y!.SchoolYear)
                        .Where(x => x.Student!.SchoolYear!.Year == cmb_Years.Text
                                    && (x.Subject!.Name == "Mathematics" || x.Subject.Name == "Physics" ||
                                        x.Subject.Name == "Chemistry") && x.SubjectScore >= 0
                                    && x.Student.Code == a00StudentCode)
                        .Select(x => x.SubjectScore).ToListAsync();
                    var b00StudentCode =
                        await b00.Where(x => x.Sum == b00_Max).Select(x => x.Key).FirstAsync()!;
                    var b00_subjects = await context.Scores
                        .Include(x => x.Subject)
                        .Include(x => x.Student)
                        .ThenInclude(y => y!.SchoolYear)
                        .Where(x => x.Student!.SchoolYear!.Year == cmb_Years.Text
                                    && (x.Subject!.Name == "Mathematics" || x.Subject.Name == "Chemistry" ||
                                        x.Subject.Name == "Biology") && x.SubjectScore >= 0
                                    && x.Student.Code == b00StudentCode)
                        .Select(x => x.SubjectScore).ToListAsync();
                    var c00StudentCode =
                        await c00.Where(x => x.Sum == c00_Max).Select(x => x.Key).FirstAsync()!;
                    var c00_subjects = await context.Scores
                        .Include(x => x.Subject)
                        .Include(x => x.Student)
                        .ThenInclude(y => y!.SchoolYear)
                        .Where(x => x.Student!.SchoolYear!.Year == cmb_Years.Text
                                    && (x.Subject!.Name == "Literature" || x.Subject.Name == "History" ||
                                        x.Subject.Name == "Geography") && x.SubjectScore >= 0
                                    && x.Student.Code == c00StudentCode)
                        .Select(x => x.SubjectScore).ToListAsync();
                    var a01StudentCode =
                        await a01.Where(x => x.Sum == a01_Max).Select(x => x.Key).FirstAsync()!;
                    var a01_subjects = await context.Scores
                        .Include(x => x.Subject)
                        .Include(x => x.Student)
                        .ThenInclude(y => y!.SchoolYear)
                        .Where(x => x.Student!.SchoolYear!.Year == cmb_Years.Text
                                    && (x.Subject!.Name == "Mathematics" || x.Subject.Name == "Physics" ||
                                        x.Subject.Name == "English") && x.SubjectScore >= 0
                                    && x.Student.Code == a01StudentCode)
                        .Select(x => x.SubjectScore).ToListAsync();
                    var d01StudentCode =
                        await d01.Where(x => x.Sum == d01_Max).Select(x => x.Key).FirstAsync()!;
                    var d01_subjects = await context.Scores
                        .Include(x => x.Subject)
                        .Include(x => x.Student)
                        .ThenInclude(y => y!.SchoolYear)
                        .Where(x => x.Student!.SchoolYear!.Year == cmb_Years.Text
                                    && (x.Subject!.Name == "Mathematics" || x.Subject.Name == "Chemistry" ||
                                        x.Subject.Name == "English") && x.SubjectScore >= 0
                                    && x.Student.Code == d01StudentCode)
                        .Select(x => x.SubjectScore).ToListAsync();
                    
                    _valeWindow.grd_ValeStudent.ItemsSource = new List<ValedictorianStudentData>()
                    {
                        new()
                        {
                            ExamGroup = "A00",
                            StudentCode = a00StudentCode,
                            Subject1 = a00_subjects[0],
                            Subject2 = a00_subjects[1],
                            Subject3 = a00_subjects[2],
                            Total = a00_Max,
                        },
                        new()
                        {
                            ExamGroup = "B00",
                            StudentCode = b00StudentCode,
                            Subject1 = b00_subjects[0],
                            Subject2 = b00_subjects[1],
                            Subject3 = b00_subjects[2],
                            Total = b00_Max,
                        },
                        new()
                        {
                            ExamGroup = "C00",
                            StudentCode = c00StudentCode,
                            Subject1 = c00_subjects[0],
                            Subject2 = c00_subjects[1],
                            Subject3 = c00_subjects[2],
                            Total = c00_Max,
                        },
                        new()
                        {
                            ExamGroup = "A01",
                            StudentCode = a01StudentCode,
                            Subject1 = a01_subjects[0],
                            Subject2 = a01_subjects[1],
                            Subject3 = a01_subjects[2],
                            Total = a01_Max,
                        },
                        new()
                        {
                            ExamGroup = "D01",
                            StudentCode = d01StudentCode,
                            Subject1 = d01_subjects[0],
                            Subject2 = d01_subjects[1],
                            Subject3 = d01_subjects[2],
                            Total = d01_Max,
                        }
                    };
                    
                    _valeWindow.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
    }
}