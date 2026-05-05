using WhiteboxMetrix.Repositories;
using WhiteboxMetrix.Rules;
using WhiteboxMetrix.Services;
using WhiteboxMetrix.Utils;
using WhiteboxMetrix.Workflow;

namespace WhiteboxMetrix.Tests;

internal sealed class SystemFixture
{
    public InMemoryUserRepository Users { get; } = new();
    public InMemoryProductRepository Products { get; } = new();
    public InMemoryOrderRepository Orders { get; } = new();
    public InMemoryCouponRepository Coupons { get; } = new();
    public PricingAuditBuffer Audit { get; } = new();

    public UserService UserService { get; }
    public ProductService ProductService { get; }
    public RuleEngine RuleEngine { get; }
    public PricingService PricingService { get; }
    public PaymentService PaymentService { get; }
    public LoyaltyService LoyaltyService { get; }
    public OrderService OrderService { get; }
    public OrderWorkflow Workflow { get; }

    public SystemFixture()
    {
        UserService = new UserService(Users);
        ProductService = new ProductService(Products);
        RuleEngine = new RuleEngine(Coupons);
        PricingService = new PricingService(Products, RuleEngine, ProductService);
        PaymentService = new PaymentService();
        LoyaltyService = new LoyaltyService();
        OrderService = new OrderService(Orders, UserService, ProductService, PricingService, PaymentService, LoyaltyService);
        Workflow = new OrderWorkflow(OrderService, UserService, ProductService, PaymentService, Users, Audit);
    }
}
