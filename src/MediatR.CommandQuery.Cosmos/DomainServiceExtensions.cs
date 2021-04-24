﻿using System.Collections.Generic;
using Cosmos.Abstracts;
using FluentValidation;
using MediatR.CommandQuery.Behaviors;
using MediatR.CommandQuery.Commands;
using MediatR.CommandQuery.Cosmos.Behaviors;
using MediatR.CommandQuery.Cosmos.Handlers;
using MediatR.CommandQuery.Definitions;
using MediatR.CommandQuery.Extensions;
using MediatR.CommandQuery.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MediatR.CommandQuery.Cosmos
{
    public static class DomainServiceExtensions
    {
        public static IServiceCollection AddMediator(this IServiceCollection services)
        {
            // Register MediatR
            services.TryAddScoped<ServiceFactory>(p => p.GetService);
            services.TryAddScoped<IMediator, Mediator>();
            services.TryAddScoped<ISender, Mediator>();
            services.TryAddScoped<IPublisher, Mediator>();

            return services;
        }

        public static IServiceCollection AddValidatorsFromAssembly<T>(this IServiceCollection services)
        {
            // Register validators
            var scanner = AssemblyScanner.FindValidatorsInAssemblyContaining<T>();
            foreach (var scanResult in scanner)
            {
                //Register as interface
                services.TryAdd(new ServiceDescriptor(scanResult.InterfaceType, scanResult.ValidatorType, ServiceLifetime.Singleton));
                //Register as self
                services.TryAdd(new ServiceDescriptor(scanResult.ValidatorType, scanResult.ValidatorType, ServiceLifetime.Singleton));
            }

            return services;
        }


        public static IServiceCollection AddEntityQueries<TRepository, TEntity, TReadModel>(this IServiceCollection services)
            where TRepository : ICosmosRepository<TEntity>
            where TEntity : class, IHaveIdentifier<string>, new()
        {
            // standard queries
            services.TryAddScoped<IRequestHandler<EntityIdentifierQuery<string, TReadModel>, TReadModel>, EntityIdentifierQueryHandler<TRepository, TEntity, TReadModel>>();
            services.TryAddScoped<IRequestHandler<EntityIdentifiersQuery<string, TReadModel>, IReadOnlyCollection<TReadModel>>, EntityIdentifiersQueryHandler<TRepository, TEntity, TReadModel>>();
            services.TryAddScoped<IRequestHandler<EntityPagedQuery<TReadModel>, EntityPagedResult<TReadModel>>, EntityPagedQueryHandler<TRepository, TEntity, TReadModel>>();
            services.TryAddScoped<IRequestHandler<EntitySelectQuery<TReadModel>, IReadOnlyCollection<TReadModel>>, EntitySelectQueryHandler<TRepository, TEntity, TReadModel>>();

            // pipeline registration, run in order registered
            bool supportsTenant = typeof(TReadModel).Implements<IHaveTenant<string>>();
            if (supportsTenant)
            {
                services.AddScoped<IPipelineBehavior<EntityPagedQuery<TReadModel>, EntityPagedResult<TReadModel>>, TenantPagedQueryBehavior<string, TReadModel>>();
                services.AddScoped<IPipelineBehavior<EntitySelectQuery<TReadModel>, IReadOnlyCollection<TReadModel>>, TenantSelectQueryBehavior<string, TReadModel>>();
            }

            bool supportsDeleted = typeof(TReadModel).Implements<ITrackDeleted>();
            if (supportsDeleted)
            {
                services.AddScoped<IPipelineBehavior<EntityPagedQuery<TReadModel>, EntityPagedResult<TReadModel>>, DeletedPagedQueryBehavior<TReadModel>>();
                services.AddScoped<IPipelineBehavior<EntitySelectQuery<TReadModel>, IReadOnlyCollection<TReadModel>>, DeletedSelectQueryBehavior<TReadModel>>();
            }

            return services;
        }

        public static IServiceCollection AddEntityQueryMemoryCache<TRepository, TEntity, TReadModel>(this IServiceCollection services)
            where TRepository : ICosmosRepository<TEntity>
            where TEntity : class, IHaveIdentifier<string>, new()
        {
            services.AddScoped<IPipelineBehavior<EntityIdentifierQuery<string, TReadModel>, TReadModel>, MemoryCacheQueryBehavior<EntityIdentifierQuery<string, TReadModel>, TReadModel>>();
            services.AddScoped<IPipelineBehavior<EntityIdentifiersQuery<string, TReadModel>, IReadOnlyCollection<TReadModel>>, MemoryCacheQueryBehavior<EntityIdentifiersQuery<string, TReadModel>, IReadOnlyCollection<TReadModel>>>();
            services.AddScoped<IPipelineBehavior<EntityPagedQuery<TReadModel>, EntityPagedResult<TReadModel>>, MemoryCacheQueryBehavior<EntityPagedQuery<TReadModel>, EntityPagedResult<TReadModel>>>();
            services.AddScoped<IPipelineBehavior<EntitySelectQuery<TReadModel>, IReadOnlyCollection<TReadModel>>, MemoryCacheQueryBehavior<EntitySelectQuery<TReadModel>, IReadOnlyCollection<TReadModel>>>();

            return services;
        }

        public static IServiceCollection AddEntityQueryDistributedCache<TRepository, TEntity, TReadModel>(this IServiceCollection services)
            where TRepository : ICosmosRepository<TEntity>
            where TEntity : class, IHaveIdentifier<string>, new()
        {
            services.AddScoped<IPipelineBehavior<EntityIdentifierQuery<string, TReadModel>, TReadModel>, DistributedCacheQueryBehavior<EntityIdentifierQuery<string, TReadModel>, TReadModel>>();
            services.AddScoped<IPipelineBehavior<EntityIdentifiersQuery<string, TReadModel>, IReadOnlyCollection<TReadModel>>, DistributedCacheQueryBehavior<EntityIdentifiersQuery<string, TReadModel>, IReadOnlyCollection<TReadModel>>>();
            services.AddScoped<IPipelineBehavior<EntityPagedQuery<TReadModel>, EntityPagedResult<TReadModel>>, DistributedCacheQueryBehavior<EntityPagedQuery<TReadModel>, EntityPagedResult<TReadModel>>>();
            services.AddScoped<IPipelineBehavior<EntitySelectQuery<TReadModel>, IReadOnlyCollection<TReadModel>>, DistributedCacheQueryBehavior<EntitySelectQuery<TReadModel>, IReadOnlyCollection<TReadModel>>>();

            return services;
        }



        public static IServiceCollection AddEntityCommands<TRepository, TEntity, TReadModel, TCreateModel, TUpdateModel>(this IServiceCollection services)
            where TRepository : ICosmosRepository<TEntity>
            where TEntity : class, IHaveIdentifier<string>, new()
            where TCreateModel : class
            where TUpdateModel : class
        {
            services
                .AddEntityCreateCommand<TRepository, TEntity, TReadModel, TCreateModel>()
                .AddEntityUpdateCommand<TRepository, TEntity, TReadModel, TUpdateModel>()
                .AddEntityUpsertCommand<TRepository, TEntity, TReadModel, TUpdateModel>()
                .AddEntityPatchCommand<TRepository, TEntity, TReadModel>()
                .AddEntityDeleteCommand<TRepository, TEntity, TReadModel>();

            return services;
        }


        public static IServiceCollection AddEntityCreateCommand<TRepository, TEntity, TReadModel, TCreateModel>(this IServiceCollection services)
            where TRepository : ICosmosRepository<TEntity>
            where TEntity : class, IHaveIdentifier<string>, new()
            where TCreateModel : class
        {

            // standard crud commands
            services.TryAddTransient<IRequestHandler<EntityCreateCommand<TCreateModel, TReadModel>, TReadModel>, EntityCreateCommandHandler<TRepository, TEntity, TCreateModel, TReadModel>>();

            // pipeline registration, run in order registered
            var createType = typeof(TCreateModel);
            bool supportsTenant = createType.Implements<IHaveTenant<string>>();
            if (supportsTenant)
            {
                services.AddTransient<IPipelineBehavior<EntityCreateCommand<TCreateModel, TReadModel>, TReadModel>, TenantDefaultCommandBehavior<string, TCreateModel, TReadModel>>();
                services.AddTransient<IPipelineBehavior<EntityCreateCommand<TCreateModel, TReadModel>, TReadModel>, TenantAuthenticateCommandBehavior<string, TCreateModel, TReadModel>>();
            }

            bool supportsTracking = createType.Implements<ITrackCreated>();
            if (supportsTracking)
                services.AddTransient<IPipelineBehavior<EntityCreateCommand<TCreateModel, TReadModel>, TReadModel>, TrackChangeCommandBehavior<TCreateModel, TReadModel>>();

            services.AddTransient<IPipelineBehavior<EntityCreateCommand<TCreateModel, TReadModel>, TReadModel>, ValidateEntityModelCommandBehavior<TCreateModel, TReadModel>>();
            services.AddTransient<IPipelineBehavior<EntityCreateCommand<TCreateModel, TReadModel>, TReadModel>, EntityChangeNotificationBehavior<string, TCreateModel, TReadModel>>();

            return services;
        }

        public static IServiceCollection AddEntityUpdateCommand<TRepository, TEntity, TReadModel, TUpdateModel>(this IServiceCollection services)
            where TRepository : ICosmosRepository<TEntity>
            where TEntity : class, IHaveIdentifier<string>, new()
            where TUpdateModel : class
        {

            // allow query for update models
            services.TryAddTransient<IRequestHandler<EntityIdentifierQuery<string, TUpdateModel>, TUpdateModel>, EntityIdentifierQueryHandler<TRepository, TEntity, TUpdateModel>>();
            services.TryAddTransient<IRequestHandler<EntityIdentifiersQuery<string, TUpdateModel>, IReadOnlyCollection<TUpdateModel>>, EntityIdentifiersQueryHandler<TRepository, TEntity, TUpdateModel>>();

            // standard crud commands
            services.TryAddTransient<IRequestHandler<EntityUpdateCommand<string, TUpdateModel, TReadModel>, TReadModel>, EntityUpdateCommandHandler<TRepository, TEntity, TUpdateModel, TReadModel>>();

            // pipeline registration, run in order registered
            var updateType = typeof(TUpdateModel);
            bool supportsTenant = updateType.Implements<IHaveTenant<string>>();
            if (supportsTenant)
            {
                services.AddTransient<IPipelineBehavior<EntityUpdateCommand<string, TUpdateModel, TReadModel>, TReadModel>, TenantDefaultCommandBehavior<string, TUpdateModel, TReadModel>>();
                services.AddTransient<IPipelineBehavior<EntityUpdateCommand<string, TUpdateModel, TReadModel>, TReadModel>, TenantAuthenticateCommandBehavior<string, TUpdateModel, TReadModel>>();
            }

            bool supportsTracking = updateType.Implements<ITrackUpdated>();
            if (supportsTracking)
                services.AddTransient<IPipelineBehavior<EntityUpdateCommand<string, TUpdateModel, TReadModel>, TReadModel>, TrackChangeCommandBehavior<TUpdateModel, TReadModel>>();

            services.AddTransient<IPipelineBehavior<EntityUpdateCommand<string, TUpdateModel, TReadModel>, TReadModel>, ValidateEntityModelCommandBehavior<TUpdateModel, TReadModel>>();
            services.AddTransient<IPipelineBehavior<EntityUpdateCommand<string, TUpdateModel, TReadModel>, TReadModel>, EntityChangeNotificationBehavior<string, TUpdateModel, TReadModel>>();

            return services;
        }

        public static IServiceCollection AddEntityUpsertCommand<TRepository, TEntity, TReadModel, TUpdateModel>(this IServiceCollection services)
            where TRepository : ICosmosRepository<TEntity>
            where TEntity : class, IHaveIdentifier<string>, new()
            where TUpdateModel : class
        {
            // standard crud commands
            services.TryAddTransient<IRequestHandler<EntityUpsertCommand<string, TUpdateModel, TReadModel>, TReadModel>, EntityUpsertCommandHandler<TRepository, TEntity, TUpdateModel, TReadModel>>();

            // pipeline registration, run in order registered
            var updateType = typeof(TUpdateModel);
            bool supportsTenant = updateType.Implements<IHaveTenant<string>>();
            if (supportsTenant)
            {
                services.AddTransient<IPipelineBehavior<EntityUpsertCommand<string, TUpdateModel, TReadModel>, TReadModel>, TenantDefaultCommandBehavior<string, TUpdateModel, TReadModel>>();
                services.AddTransient<IPipelineBehavior<EntityUpsertCommand<string, TUpdateModel, TReadModel>, TReadModel>, TenantAuthenticateCommandBehavior<string, TUpdateModel, TReadModel>>();
            }

            bool supportsTracking = updateType.Implements<ITrackUpdated>();
            if (supportsTracking)
                services.AddTransient<IPipelineBehavior<EntityUpsertCommand<string, TUpdateModel, TReadModel>, TReadModel>, TrackChangeCommandBehavior<TUpdateModel, TReadModel>>();

            services.AddTransient<IPipelineBehavior<EntityUpsertCommand<string, TUpdateModel, TReadModel>, TReadModel>, ValidateEntityModelCommandBehavior<TUpdateModel, TReadModel>>();
            services.AddTransient<IPipelineBehavior<EntityUpsertCommand<string, TUpdateModel, TReadModel>, TReadModel>, EntityChangeNotificationBehavior<string, TUpdateModel, TReadModel>>();

            return services;
        }

        public static IServiceCollection AddEntityPatchCommand<TRepository, TEntity, TReadModel>(this IServiceCollection services)
            where TRepository : ICosmosRepository<TEntity>
            where TEntity : class, IHaveIdentifier<string>, new()
        {
            // standard crud commands
            services.TryAddTransient<IRequestHandler<EntityPatchCommand<string, TReadModel>, TReadModel>, EntityPatchCommandHandler<TRepository, TEntity, TReadModel>>();

            // pipeline registration, run in order registered
            services.AddTransient<IPipelineBehavior<EntityPatchCommand<string, TReadModel>, TReadModel>, EntityChangeNotificationBehavior<string, TEntity, TReadModel>>();

            return services;
        }

        public static IServiceCollection AddEntityDeleteCommand<TRepository, TEntity, TReadModel>(this IServiceCollection services)
            where TRepository : ICosmosRepository<TEntity>
            where TEntity : class, IHaveIdentifier<string>, new()
        {

            // standard crud commands
            services.TryAddTransient<IRequestHandler<EntityDeleteCommand<string, TReadModel>, TReadModel>, EntityDeleteCommandHandler<TRepository, TEntity, TReadModel>>();

            // pipeline registration, run in order registered
            services.AddTransient<IPipelineBehavior<EntityDeleteCommand<string, TReadModel>, TReadModel>, EntityChangeNotificationBehavior<string, TEntity, TReadModel>>();

            return services;
        }
    }
}
