using FluentValidation;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Common.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            var firstError = failures.First();
            var error = Error.Validation(firstError.PropertyName, firstError.ErrorMessage);

            if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var resultType = typeof(TResponse).GetGenericArguments()[0];
                var failureMethod = typeof(Result).GetMethods()
                    .First(m => m.Name == "Failure" && m.IsGenericMethod && m.GetParameters().Length == 1)
                    .MakeGenericMethod(resultType);
                return (TResponse)failureMethod.Invoke(null, new object[] { error })!;
            }

            if (typeof(TResponse) == typeof(Result))
                return (TResponse)(object)Result.Failure(error);

            throw new ValidationException(failures);
        }

        return await next();
    }
}
