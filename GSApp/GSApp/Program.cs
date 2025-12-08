using System;

// Домен

class Applicant
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Fio { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }

    public Application PrepareApplication(ApplicationData data)
    {
        var app = new Application(this.Id, data);
        app.ChangeStatusToSubmitted();
        Console.WriteLine($"Соискатель {Fio} подал заявку '{data.Title}'");
        return app;
    }
}

// Цепочка обязанностей
interface IEvaluator
{
    void SetNext(IEvaluator next);
    void Evaluate(Application app);
}

class Expert : IEvaluator
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Fio { get; set; }
    public string Specialization { get; set; }
    public string Degree { get; set; }

    protected IEvaluator next;

    public void SetNext(IEvaluator next) => this.next = next;

    public virtual void Evaluate(Application app)
    {
        var score = new Random().Next(1, 11);
        var eval = new Evaluation(app.Id, this.Id, score, $"Оценка от: {Fio}");
        app.AttachEvaluation(eval);

        Console.WriteLine($"Эксперт {Fio} оценил заявку '{app.Data.Title}'. Оценка - {score}");

        next?.Evaluate(app);
    }
}

class SpecificExpertN : Expert
{
    public override void Evaluate(Application app)
    {
        int score = new Random().Next(5, 11); // более высокая оценка
        var eval = new Evaluation(app.Id, this.Id, score, $"Конкретный эксперт {Fio}");
        app.AttachEvaluation(eval);

        Console.WriteLine($"КонкретныйЭксперт {Fio} оценил '{app.Data.Title}'. Оценка - {score}");

        next?.Evaluate(app);
    }
}

class Founder
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Fio { get; set; }

    public void MakeDecision(Application app, string result, decimal grantAmount)
    {
        var dec = new Decision { Result = result, GrantAmount = grantAmount, Report = $"Решение от: {Fio}" };
        app.AttachDecision(dec);
        Console.WriteLine($"Держатель фонда {Fio} принял решение '{result}' по заявке '{app.Data.Title}'");
    }
}

class Application
{
    public Guid Id { get; } = Guid.NewGuid();
    public Guid ApplicantId { get; }
    public ApplicationData Data { get; private set; }
    public string Status { get; private set; } = "Черновик";

    public Grant Grant { get; set; }
    public Decision Decision { get; private set; }
    public List<Evaluation> Evaluations { get; } = new();

    public Application(Guid applicantId, ApplicationData data)
    {
        ApplicantId = applicantId;
        Data = data;
    }

    public void ChangeStatusToSubmitted() => Status = "Подана";
    public void Edit(ApplicationData data) { if (Status == "Черновик") Data = data; }
    public void Withdraw() => Status = "Отозвана";
    public void AttachEvaluation(Evaluation ev) { Evaluations.Add(ev); Status = "На проверке"; }
    public void AttachDecision(Decision dec) { Decision = dec; Status = "Решение принято"; }
}

class Grant
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Title { get; set; }
    public string Description { get; set; }
    public decimal MaxAmount { get; set; }
}

class Evaluation
{
    public Guid Id { get; } = Guid.NewGuid();
    public Guid ApplicationId { get; }
    public Guid ExpertId { get; }
    public int Score { get; }
    public string Comments { get; }

    public Evaluation(Guid appId, Guid expertId, int score, string comments)
    {
        ApplicationId = appId;
        ExpertId = expertId;
        Score = score;
        Comments = comments;
    }
}

class Decision
{
    public decimal GrantAmount { get; set; }
    public string Result { get; set; }
    public string Report { get; set; }
}

// Стратегия (выбор экспертов)
// добавить на диаграмму
interface IExpertSelectionStrategy
{
    List<IEvaluator> Select(List<IEvaluator> allExperts, Application app);
}

// Стратегия 1 - по специализации
// эксперты, чья специализация встречается в названии заявки.
class BySpecializationStrategy : IExpertSelectionStrategy
{
    public List<IEvaluator> Select(List<IEvaluator> allExperts, Application app)
    {
        var list = new List<IEvaluator>();

        foreach (var e in allExperts)
        {
            if (e is Expert ex &&
                app.Data.Title.ToLower().Contains(ex.Specialization.ToLower()))
            {
                list.Add(e);
            }
        }

        if (list.Count == 0)
            list.Add(allExperts[0]); // fallback (если ни один эксперт не подошёл, берём первого эксперта из списка всех)

        return list;
    }
}

// Фабрика
// добавить на диаграмму
interface IExpertFactory
{
    IEvaluator CreateExpert(string fio, string specialization, string degree);
}

class ExpertFactory : IExpertFactory
{
    public IEvaluator CreateExpert(string fio, string specialization, string degree)
    {
        // бизнес-правило. если "PhD" => specific expert
        if (degree == "PhD")
            return new SpecificExpertN { Fio = fio, Specialization = specialization, Degree = degree };

        return new Expert { Fio = fio, Specialization = specialization, Degree = degree };
    }
}

class GrantSystemService
{
    private readonly IExpertSelectionStrategy strategy;

    public GrantSystemService(IExpertSelectionStrategy strategy)
    {
        this.strategy = strategy;
    }

    public List<IEvaluator> AllExperts { get; } = new();
    public List<Application> Applications { get; } = new();

    public Application SubmitApplication(Applicant applicant, ApplicationData data)
    {
        var app = applicant.PrepareApplication(data);
        Applications.Add(app);
        return app;
    }

    public List<IEvaluator> AssignExperts(Application app)
    {
        var selected = strategy.Select(AllExperts, app);

        for (int i = 0; i < selected.Count - 1; i++)
            selected[i].SetNext(selected[i + 1]);

        Console.WriteLine($"Эксперты назначены (по стратегии): {selected.Count}");

        return selected;
    }

    public void StartEvaluation(Application app, IEvaluator first)
    {
        first.Evaluate(app);
    }

    public void MakeDecision(Application app, Founder founder, string result, decimal grantAmount)
    {
        founder.MakeDecision(app, result, grantAmount);
    }
}
class ApplicationData
{
    public string Title { get; set; }
    public string Description { get; set; }
    public decimal RequestedAmount { get; set; }
}

// Пример использования
class Program
{
    static void Main(string[] args)
    {
        var strategy = new BySpecializationStrategy();
        var factory = new ExpertFactory();
        var service = new GrantSystemService(strategy);

        // создаем экспертов (фабрика)
        service.AllExperts.Add(factory.CreateExpert("к.б.н. Густаво", "bio", "PhD"));
        service.AllExperts.Add(factory.CreateExpert("к.б.н. Джесси", "bio", "Master"));
        service.AllExperts.Add(factory.CreateExpert("к.х.н. Уолтер", "chemistry", "PhD"));

        var applicant = new Applicant { Fio = "Иван Петров", Email = "ivan@example.com" };
        var data = new ApplicationData { Title = "[Bio] Проект исследования молекулярных часов", Description = "Исследование", RequestedAmount = 50000 };

        var app = service.SubmitApplication(applicant, data);

        var selected = service.AssignExperts(app);
        service.StartEvaluation(app, selected[0]);

        var founder = new Founder { Fio = "Хаус МД" };
        service.MakeDecision(app, founder, "Одобрено", 40000);
    }
}
