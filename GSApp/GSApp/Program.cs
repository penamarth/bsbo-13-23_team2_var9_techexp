using System;

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

        Console.WriteLine($"Эксперт {Fio} оценил заявку '{app.Data.Title}'. Оценка = {score}");

        next?.Evaluate(app);
    }
}

class SpecificExpertN : Expert
{
    public override void Evaluate(Application app)
    {
        var score = new Random().Next(5, 11);
        var eval = new Evaluation(app.Id, this.Id, score, $"Эксперт высокого уровня: {Fio}");
        app.AttachEvaluation(eval);

        Console.WriteLine($"SpecificExpert {Fio} оценил '{app.Data.Title}'. Оценка = {score}");

        next?.Evaluate(app);
    }
}

class Founder
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Fio { get; set; }

    public GrantDecision MakeDecision(Application app, string result, decimal grantAmount)
    {
        var dec = new GrantDecision
        {
            Title = $"Решение по заявке {app.Id}",
            Result = result,
            GrantAmount = grantAmount,
            Report = $"Решение от держателя фонда: {Fio}"
        };

        app.AttachDecision(dec);

        Console.WriteLine($"Держатель фонда {Fio} принял решение '{result}'");

        return dec;
    }
}

class Application
{
    public Guid Id { get; } = Guid.NewGuid();
    public Guid ApplicantId { get; }
    public ApplicationData Data { get; private set; }

    public string Status { get; private set; } = "Черновик";

    public GrantDecision Decision { get; private set; }
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
    public void AttachDecision(GrantDecision dec) { Decision = dec; Status = "Решение принято"; }
}

class ApplicationData
{
    public string Title { get; set; }
    public string Description { get; set; }
    public decimal RequestedAmount { get; set; }
}

class GrantDecision
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Title { get; set; }
    public string Result { get; set; }
    public decimal GrantAmount { get; set; }
    public string Report { get; set; }
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


interface IExpertSelectionStrategy
{
    List<IEvaluator> Select(List<IEvaluator> allExperts, Application app);
}

class BySpecializationStrategy : IExpertSelectionStrategy
{
    public List<IEvaluator> Select(List<IEvaluator> allExperts, Application app)
    {
        var result = new List<IEvaluator>();

        foreach (var e in allExperts)
        {
            if (e is Expert ex &&
                app.Data.Title.ToLower().Contains(ex.Specialization.ToLower()))
            {
                result.Add(e);
            }
        }

        if (result.Count == 0 && allExperts.Count > 0)
            result.Add(allExperts[0]);

        return result;
    }
}


interface IExpertFactory
{
    IEvaluator CreateExpert(string fio, string specialization, string degree);
}

class ExpertFactory : IExpertFactory
{
    public IEvaluator CreateExpert(string fio, string specialization, string degree)
    {
        if (degree == "PhD")
            return new SpecificExpertN { Fio = fio, Specialization = specialization, Degree = degree };

        return new Expert { Fio = fio, Specialization = specialization, Degree = degree };
    }
}

interface IApplicationRepository
{
    void Save(Application app);
    void Update(Application app);
    void Delete(Application app);
    Application FindById(Guid id);
    List<Application> FindByStatus(string status);
}

interface IWorkersRepository
{
    void SaveExpert(Expert expert);
    void SaveFounder(Founder founder);
    Expert FindExpertById(Guid id);
    Founder FindFounderById(Guid id);
}

interface IApplicantRepository
{
    void Save(Applicant applicant);
    Applicant FindById(Guid id);
}


class GrantSystemService
{
    private readonly IExpertSelectionStrategy strategy;
    private readonly IApplicationRepository appRepo;
    private readonly IWorkersRepository workerRepo;
    private readonly IApplicantRepository applicantRepo;
    private readonly IExpertFactory expertFactory;

    public List<IEvaluator> AllExperts { get; } = new();

    public GrantSystemService(
        IExpertSelectionStrategy strategy,
        IApplicationRepository appRepo,
        IWorkersRepository workerRepo,
        IApplicantRepository applicantRepo,
        IExpertFactory expertFactory)
    {
        this.strategy = strategy;
        this.appRepo = appRepo;
        this.workerRepo = workerRepo;
        this.applicantRepo = applicantRepo;
        this.expertFactory = expertFactory;
    }

    public Application SubmitApplication(Guid applicantId, ApplicationData data)
    {
        var applicant = applicantRepo.FindById(applicantId);
        var app = applicant.PrepareApplication(data);

        appRepo.Save(app);

        return app;
    }

    public List<IEvaluator> AssignExperts(Application app)
    {
        var selected = strategy.Select(AllExperts, app);

        for (int i = 0; i < selected.Count - 1; i++)
            selected[i].SetNext(selected[i + 1]);

        return selected;
    }

    public void StartEvaluation(Guid applicationId, Guid expertId)
    {
        var app = appRepo.FindById(applicationId);
        var expert = workerRepo.FindExpertById(expertId);

        expert.Evaluate(app);

        appRepo.Update(app);
    }

    public GrantDecision MakeDecision(Guid applicationId, Guid founderId, string result, decimal amount)
    {
        var app = appRepo.FindById(applicationId);
        var founder = workerRepo.FindFounderById(founderId);

        var decision = founder.MakeDecision(app, result, amount);

        appRepo.Update(app);

        return decision;
    }
}

class InMemoryApplicationRepository : IApplicationRepository
{
    private readonly List<Application> list = new();

    public void Save(Application app) => list.Add(app);
    public void Update(Application app) { }
    public void Delete(Application app) => list.Remove(app);
    public Application FindById(Guid id) => list.Find(x => x.Id == id);
    public List<Application> FindByStatus(string status) =>
        list.FindAll(x => x.Status == status);
}

class InMemoryWorkersRepository : IWorkersRepository
{
    private readonly List<Expert> experts = new();
    private readonly List<Founder> founders = new();

    public void SaveExpert(Expert expert) => experts.Add(expert);
    public void SaveFounder(Founder founder) => founders.Add(founder);

    public Expert FindExpertById(Guid id) => experts.Find(x => x.Id == id);
    public Founder FindFounderById(Guid id) => founders.Find(x => x.Id == id);
}

class InMemoryApplicantRepository : IApplicantRepository
{
    private readonly List<Applicant> list = new();

    public void Save(Applicant applicant) => list.Add(applicant);
    public Applicant FindById(Guid id) => list.Find(x => x.Id == id);
}

class Program
{
    static void Main()
    {
        var strategy = new BySpecializationStrategy();
        var factory = new ExpertFactory();

        var appRepo = new InMemoryApplicationRepository();
        var workerRepo = new InMemoryWorkersRepository();
        var applicantRepo = new InMemoryApplicantRepository();

        var service = new GrantSystemService(
            strategy,
            appRepo,
            workerRepo,
            applicantRepo,
            factory);

        var applicant = new Applicant
        {
            Fio = "Иван Петров",
            Email = "ivan@example.com"
        };
        applicantRepo.Save(applicant);

        var expert1 = (Expert)factory.CreateExpert("к.б.н. Густаво", "bio", "PhD");
        var expert2 = (Expert)factory.CreateExpert("к.б.н. Джесси", "bio", "Master");
        var expert3 = (Expert)factory.CreateExpert("к.х.н. Уолтер", "chemistry", "PhD");

        workerRepo.SaveExpert(expert1);
        workerRepo.SaveExpert(expert2);
        workerRepo.SaveExpert(expert3);

        var founder = new Founder { Fio = "Хаус МД" };
        workerRepo.SaveFounder(founder);

        var data = new ApplicationData
        {
            Title = "[Bio] Исследование молекулярных механизмов сна",
            Description = "Научный проект",
            RequestedAmount = 100000
        };

        var app = service.SubmitApplication(applicant.Id, data);

        service.AllExperts.Add(expert1);
        service.AllExperts.Add(expert2);
        service.AllExperts.Add(expert3);

        var selected = service.AssignExperts(app);

        // Console.WriteLine("\n--- Запуск цепочки экспертиз ---\n");
        service.StartEvaluation(app.Id, ((Expert)selected[0]).Id);

        service.MakeDecision(app.Id, founder.Id, "Одобрено", 75000);
    }
}
