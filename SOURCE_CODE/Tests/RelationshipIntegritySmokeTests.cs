using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Validation;

namespace HVAC_Pro_Desktop.Tests
{
    public static class RelationshipIntegritySmokeTests
    {
        public static List<string> RunAll()
        {
            var passed = new List<string>();

            if (RelationshipIntegrityRepository.MonitoredRelationshipCount != 100)
                throw new InvalidOperationException("Expected the cross-module relationship catalog to contain 100 established relationships.");
            passed.Add("all 100 cross-module relationships are registered");

            ExpectNoError(RelationshipIntegrityService.EvaluateClientSite(10, 25, 10, "Jobs"), "same-client site");
            passed.Add("same-client site relationship accepted");

            ExpectError(RelationshipIntegrityService.EvaluateClientSite(10, 25, 11, "Jobs"), "cross-client site");
            passed.Add("cross-client site relationship rejected");

            bool blocked = false;
            try
            {
                RelationshipIntegrityService.EnsureValid(RelationshipIntegrityService.EvaluateClientSite(10, 25, 11, "Jobs"), "Cannot save test record.");
            }
            catch (InvalidOperationException)
            {
                blocked = true;
            }
            if (!blocked)
                throw new InvalidOperationException("Relationship validation did not block an invalid save.");
            passed.Add("invalid relationship blocks save");

            ExpectError(RelationshipIntegrityService.EvaluateClientSite(10, 999, null, "Invoices"), "missing site");
            passed.Add("missing site relationship rejected");

            ExpectNoError(RelationshipIntegrityService.EvaluateClientSite(10, null, null, "Quotations"), "optional blank site");
            passed.Add("optional blank site relationship accepted");

            var jobsToSites = new RelationshipHealthRow
            {
                ChildRows = 137,
                PopulatedKeys = 129,
                MatchedKeys = 129,
                ContextViolations = 76
            };
            ExpectEqual(94.16m, jobsToSites.CoveragePercent, "job/site coverage");
            ExpectEqual(100m, jobsToSites.IntegrityPercent, "job/site direct integrity");
            ExpectEqual(41.09m, jobsToSites.ContextConsistencyPercent, "job/site context consistency");
            passed.Add("relationship coverage and consistency calculations verified");

            var snapshot = new RelationshipHealthSnapshot
            {
                Rows = new List<RelationshipHealthRow>
                {
                    new RelationshipHealthRow { PopulatedKeys = 2257, MatchedKeys = 2244, OrphanKeys = 13, ContextViolations = 175, IsForeignKeyEnforced = true, RequiresContextProtection = true, IsContextProtectionEnforced = false },
                    new RelationshipHealthRow { PopulatedKeys = 0, MatchedKeys = 0, IsForeignKeyEnforced = false },
                    new RelationshipHealthRow { PopulatedKeys = 0, MatchedKeys = 0, IsForeignKeyEnforced = true }
                }
            };
            ExpectEqual(99.42m, snapshot.WeightedIntegrityPercent, "weighted integrity");
            if (snapshot.MissingForeignKeys != 1 || snapshot.OrphanKeys != 13 || snapshot.ContextViolations != 175)
                throw new InvalidOperationException("Relationship snapshot totals were not calculated correctly.");
            if (snapshot.ConnectedRelationships != 1 || snapshot.MissingProtections != 2 || snapshot.RelationshipsNeedingReview != 2)
                throw new InvalidOperationException("Relationship protection-card totals were not calculated correctly.");
            ExpectEqual(33.33m, snapshot.ProtectionPercent, "relationship protection");
            passed.Add("relationship health snapshot totals verified");
            passed.Add("relationship protection card calculations verified");

            return passed;
        }

        private static void ExpectError(ValidationResult result, string name)
        {
            if (result == null || !result.HasErrors)
                throw new InvalidOperationException("Expected validation error for " + name + ".");
        }

        private static void ExpectNoError(ValidationResult result, string name)
        {
            if (result != null && result.HasErrors)
                throw new InvalidOperationException("Expected no validation error for " + name + ".");
        }

        private static void ExpectEqual(decimal expected, decimal actual, string name)
        {
            if (expected != actual)
                throw new InvalidOperationException("Expected " + name + " to be " + expected + " but found " + actual + ".");
        }
    }
}
