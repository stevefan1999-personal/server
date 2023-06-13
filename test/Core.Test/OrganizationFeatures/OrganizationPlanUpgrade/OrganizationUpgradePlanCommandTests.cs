﻿using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade;
using Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade.Interface;
using Bit.Core.OrganizationFeatures.OrganizationSignUp.Interfaces;
using Bit.Core.Services;
using Bit.Core.Models.StaticStore;
using Bit.Core.Tools.Services;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationPlanUpgrade;

[SutProviderCustomize]

public class OrganizationUpgradePlanCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpgradePlanAsync_FeatureFlagEnabled_SecretsManagerValidationCalled(SutProvider<OrganizationUpgradePlanCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var upgrade = new OrganizationUpgrade
        {
            Plan = PlanType.EnterpriseAnnually
        };

        var organization = new Organization
        {
            Id = organizationId,
            GatewayCustomerId = "gateway-customer-id",
            GatewaySubscriptionId = null,
            PlanType = PlanType.TeamsAnnually,
            UseSecretsManager = true,
        };
        
        var existingPlan = new Plan()
        {
            Type = PlanType.TeamsAnnually,
            BitwardenProduct = BitwardenProductType.PasswordManager,
        };

        var newPlans = new List<Plan>
        {
            new()
            {
                Type = PlanType.EnterpriseAnnually,
                BaseSeats = 2,
                BitwardenProduct = BitwardenProductType.PasswordManager,
            
            },
            new()
            {
                Type = PlanType.EnterpriseAnnually,
                BaseSeats = 2,
                BaseServiceAccount = 2,
                BitwardenProduct = BitwardenProductType.SecretsManager,
            
            }
        };


        var organizationUpgradeQuery = sutProvider.GetDependency<IOrganizationUpgradeQuery>();
        organizationUpgradeQuery.GetOrgById(organizationId).Returns(organization);
        organizationUpgradeQuery.ExistingPlan(PlanType.TeamsAnnually).Returns(existingPlan);
        organizationUpgradeQuery.NewPlans(PlanType.EnterpriseAnnually).Returns(newPlans);

        var validateUpgradeCommand = sutProvider.GetDependency<IValidateUpgradeCommand>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var referenceEventService = sutProvider.GetDependency<IReferenceEventService>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        var featureService = sutProvider.GetDependency<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.SecretManagerGaBilling, currentContext)
            .Returns(true); // Enable the feature flag
        var paymentService = Substitute.For<IPaymentService>();
        var organizationSignUpValidationStrategy = Substitute.For<IOrganizationSignUpValidationStrategy>();

        var command = new OrganizationUpgradePlanCommand(
            organizationUpgradeQuery,
            validateUpgradeCommand,
            organizationService,
            referenceEventService,
            currentContext,
            featureService,
            paymentService,
            organizationSignUpValidationStrategy
        );

        await command.UpgradePlanAsync(organizationId, upgrade);

        await validateUpgradeCommand.Received(1)
            .ValidateSmSeatsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<OrganizationUpgrade>());
        await validateUpgradeCommand.Received(1).ValidateServiceAccountAsync(Arg.Any<Organization>(), Arg.Any<Plan>(),
            Arg.Any<OrganizationUpgrade>());
    }
    
    [Theory]
    [BitAutoData]
    public async Task UpgradePlanAsync_ExistingSubscription_ThrowsBadRequestException(SutProvider<OrganizationUpgradePlanCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var upgrade = new OrganizationUpgrade
        {
            Plan = PlanType.EnterpriseAnnually,
        };
        var existingPlan = new Plan()
        {
            Type = PlanType.TeamsAnnually,
            BitwardenProduct = BitwardenProductType.PasswordManager,
        };

        var newPlans = new List<Plan>
        {
            new()
            {
                Type = PlanType.EnterpriseAnnually,
                BaseSeats = 2,
                BitwardenProduct = BitwardenProductType.PasswordManager,
            
            },
            new()
            {
                Type = PlanType.EnterpriseAnnually,
                BaseSeats = 2,
                BaseServiceAccount = 2,
                BitwardenProduct = BitwardenProductType.SecretsManager,
            
            }
        };

        var organization = new Organization
        {
            Id = organizationId,
            GatewayCustomerId = "gateway-customer-id",
            GatewaySubscriptionId = "existing-subscription-id",
            PlanType = PlanType.Free,
        };

        var organizationUpgradeQuery = sutProvider.GetDependency<IOrganizationUpgradeQuery>();
        organizationUpgradeQuery.GetOrgById(organizationId).Returns(organization);
        organizationUpgradeQuery.ExistingPlan(PlanType.TeamsAnnually).Returns(existingPlan);
        organizationUpgradeQuery.NewPlans(PlanType.EnterpriseAnnually).Returns(newPlans);

        var validateUpgradeCommand = sutProvider.GetDependency<IValidateUpgradeCommand>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var referenceEventService = sutProvider.GetDependency<IReferenceEventService>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        var featureService = sutProvider.GetDependency<IFeatureService>();
        var paymentService = sutProvider.GetDependency<IPaymentService>();
        var organizationSignUpValidationStrategy = sutProvider.GetDependency<IOrganizationSignUpValidationStrategy>();

        var command = new OrganizationUpgradePlanCommand(
            organizationUpgradeQuery,
            validateUpgradeCommand,
            organizationService,
            referenceEventService,
            currentContext,
            featureService,
            paymentService,
            organizationSignUpValidationStrategy
        );

        await Assert.ThrowsAsync<BadRequestException>(() => command.UpgradePlanAsync(organizationId, upgrade));
    }
    
    [Theory]
    [BitAutoData]
    public async Task UpgradePlanAsync_NoPaymentMethodAvailable_ThrowsBadRequestException(SutProvider<OrganizationUpgradePlanCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var upgrade = new OrganizationUpgrade
        {
            Plan = PlanType.EnterpriseAnnually,
        };

        var organization = new Organization
        {
            Id = organizationId,
            GatewayCustomerId = null, 
        };
        
        var existingPlan = new Plan()
        {
            Type = PlanType.TeamsAnnually,
            BitwardenProduct = BitwardenProductType.PasswordManager,
        };

        var newPlans = new List<Plan>
        {
            new()
            {
                Type = PlanType.EnterpriseAnnually,
                BaseSeats = 2,
                BitwardenProduct = BitwardenProductType.PasswordManager,
            
            },
            new()
            {
                Type = PlanType.EnterpriseAnnually,
                BaseSeats = 2,
                BaseServiceAccount = 2,
                BitwardenProduct = BitwardenProductType.SecretsManager,
            
            }
        };

        var organizationUpgradeQuery = sutProvider.GetDependency<IOrganizationUpgradeQuery>();
        organizationUpgradeQuery.GetOrgById(organizationId).Returns(organization);
        organizationUpgradeQuery.ExistingPlan(PlanType.TeamsAnnually).Returns(existingPlan);
        organizationUpgradeQuery.NewPlans(PlanType.EnterpriseAnnually).Returns(newPlans);

        var validateUpgradeCommand = sutProvider.GetDependency<IValidateUpgradeCommand>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var referenceEventService = sutProvider.GetDependency<IReferenceEventService>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        var featureService = sutProvider.GetDependency<IFeatureService>();
        var paymentService = sutProvider.GetDependency<IPaymentService>();
        var organizationSignUpValidationStrategy = sutProvider.GetDependency<IOrganizationSignUpValidationStrategy>();

        var command = new OrganizationUpgradePlanCommand(
            organizationUpgradeQuery,
            validateUpgradeCommand,
            organizationService,
            referenceEventService,
            currentContext,
            featureService,
            paymentService,
            organizationSignUpValidationStrategy
        );

        await Assert.ThrowsAsync<BadRequestException>(() => command.UpgradePlanAsync(organizationId, upgrade));
    }
}
