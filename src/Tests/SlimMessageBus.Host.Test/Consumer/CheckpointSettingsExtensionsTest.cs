﻿using System;
using FluentAssertions;
using SlimMessageBus.Host.Config;
using Xunit;

namespace SlimMessageBus.Host.Test
{
    public class CheckpointSettingsExtensionsTest
    {
        [Fact]
        public void ConsumerSettings_SetsCheckpointsProperly()
        {
            // arrange
            var cs = new ConsumerSettings();

            // act
            cs.CheckpointEvery(10);
            cs.CheckpointAfter(TimeSpan.FromHours(60));

            // assert
            cs.Properties[CheckpointSettings.CheckpointCount].Should().BeEquivalentTo(10);
            cs.Properties[CheckpointSettings.CheckpointDuration].Should().BeEquivalentTo(TimeSpan.FromHours(60));
        }

        [Fact]
        public void RequestResponseSettings_SetsCheckpointsProperly()
        {
            // arrange
            var cs = new RequestResponseSettings();

            // act
            cs.CheckpointEvery(10);
            cs.CheckpointAfter(TimeSpan.FromHours(60));

            // assert
            cs.Properties[CheckpointSettings.CheckpointCount].Should().BeEquivalentTo(10);
            cs.Properties[CheckpointSettings.CheckpointDuration].Should().BeEquivalentTo(TimeSpan.FromHours(60));
        }
    }
}
