using DevInterview.Application.Common;
using DevInterview.Application.Features.Admin;
using DevInterview.Application.Features.Ai;
using DevInterview.Application.Features.Analytics;
using DevInterview.Application.Features.Auth;
using DevInterview.Application.Features.Coding;
using DevInterview.Application.Features.Companies;
using DevInterview.Application.Features.Flashcards;
using DevInterview.Application.Features.Gamification;
using DevInterview.Application.Features.Learning;
using DevInterview.Application.Features.Library;
using DevInterview.Application.Features.MockInterviews;
using DevInterview.Application.Features.Notifications;
using DevInterview.Application.Features.Questions;
using DevInterview.Application.Features.Quizzes;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Application.Features.Users;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevInterview.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PlanLimitsOptions>(configuration.GetSection(PlanLimitsOptions.Section));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.Section));
        services.Configure<AppUrlOptions>(configuration.GetSection(AppUrlOptions.Section));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<IGamificationService, GamificationService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<QuestionService>();
        services.AddScoped<IQuestionService>(sp => sp.GetRequiredService<QuestionService>());
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<CodingService>();
        services.AddScoped<ICodingService>(sp => sp.GetRequiredService<CodingService>());
        services.AddScoped<IAiOrchestrator, AiOrchestrator>();
        services.AddScoped<IPromptProvider, PromptProvider>();
        services.AddScoped<IAiToolsService, AiToolsService>();
        services.AddScoped<IMockInterviewService, MockInterviewService>();
        services.AddScoped<ILearningService, LearningService>();
        services.AddScoped<IFlashcardService, FlashcardService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ICollectionService, CollectionService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReminderJobService, ReminderJobService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IAdminContentService, AdminContentService>();
        services.AddScoped<IAdminCatalogService, AdminCatalogService>();
        services.AddScoped<IAdminOperationsService, AdminOperationsService>();
        return services;
    }
}
