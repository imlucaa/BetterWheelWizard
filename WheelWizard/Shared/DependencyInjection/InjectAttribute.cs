namespace WheelWizard.Shared.DependencyInjection;

/// <summary>
/// Attribute to mark properties for dependency injection.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class InjectAttribute : Attribute;
