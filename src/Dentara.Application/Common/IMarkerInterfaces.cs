namespace Dentara.Application.Common;

using Dentara.Application.Common.Results;
using MediatR;

/// <summary>
/// Marker interface for commands (write-side requests). Commands return a result;
/// the result is either a success with payload or a failure with error code+message.
/// </summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}

/// <summary>
/// Marker interface for queries (read-side requests). Queries never mutate state
/// and always return a Result.
/// </summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
