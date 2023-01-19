﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Logging;

namespace Microsoft.IdentityModel.Tokens
{
    /// <summary>
    /// Represents a generic configuration manager.
    /// </summary>
    public abstract class BaseConfigurationManager
    {
        private TimeSpan _automaticRefreshInterval = DefaultAutomaticRefreshInterval;
        private TimeSpan _refreshInterval = DefaultRefreshInterval;
        private TimeSpan _lastKnownGoodLifetime = DefaultLastKnownGoodConfigurationLifetime;
        private BaseConfiguration _lastKnownGoodConfiguration;
        private DateTime? _lastKnownGoodConfigFirstUse = null;

        private Dictionary<BaseConfiguration, DateTime> _lkgConfigurationCache = null;
        private IEqualityComparer<BaseConfiguration> _baseConfigurationComparer = new BaseConfigurationComparer();

        /// <summary>
        /// Gets or sets the <see cref="TimeSpan"/> that controls how often an automatic metadata refresh should occur.
        /// </summary>
        public TimeSpan AutomaticRefreshInterval
        {
            get { return _automaticRefreshInterval; }
            set
            {
                if (value < MinimumAutomaticRefreshInterval)
                    throw LogHelper.LogExceptionMessage(new ArgumentOutOfRangeException(nameof(value), LogHelper.FormatInvariant(LogMessages.IDX10108, LogHelper.MarkAsNonPII(MinimumAutomaticRefreshInterval), LogHelper.MarkAsNonPII(value))));

                _automaticRefreshInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets the BaseConfgiurationComparer that to compare <see cref="BaseConfiguration"/>.
        /// </summary>
        public IEqualityComparer<BaseConfiguration> BaseConfigurationComparer
        {
            get { return _baseConfigurationComparer; }
            set
            {
                _baseConfigurationComparer = value ?? throw LogHelper.LogExceptionMessage(new ArgumentNullException(nameof(value)));
            }
        }

        /// <summary>
        /// 12 hours is the default time interval that afterwards will obtain new configuration.
        /// </summary>
        public static readonly TimeSpan DefaultAutomaticRefreshInterval = new TimeSpan(0, 12, 0, 0);

        /// <summary>
        /// 1 hour is the default time interval that a last known good configuration will last for.
        /// </summary>
        public static readonly TimeSpan DefaultLastKnownGoodConfigurationLifetime = new TimeSpan(0, 1, 0, 0);

        /// <summary>
        /// 5 minutes is the default time interval that must pass for <see cref="RequestRefresh"/> to obtain a new configuration.
        /// </summary>
        public static readonly TimeSpan DefaultRefreshInterval = new TimeSpan(0, 0, 5, 0);

        /// <summary>
        /// Obtains an updated version of <see cref="BaseConfiguration"/> if the appropriate refresh interval has passed.
        /// This method may return a cached version of the configuration.
        /// </summary>
        /// <param name="cancel">CancellationToken</param>
        /// <returns>Configuration of type Configuration.</returns>
        /// <remarks>This method on the base class throws a <see cref="NotImplementedException"/> as it is meant to be
        /// overridden by the class that extends it.</remarks>
        public virtual Task<BaseConfiguration> GetBaseConfigurationAsync(CancellationToken cancel)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets all valid last known good configurations from the cache.
        /// </summary>
        /// <returns>A collection of all valid last known good configurations.</returns>
        internal ICollection<BaseConfiguration> GetValidLkgConfiguraitonFromCache()
        {
            if (_lkgConfigurationCache == null)
                return null;

            var expiredLkgConfiguration = _lkgConfigurationCache.Where(x => x.Value < DateTime.UtcNow).ToArray();

            foreach (KeyValuePair<BaseConfiguration, DateTime> lkgConfiguration in expiredLkgConfiguration)
            {
                _lkgConfigurationCache.Remove(lkgConfiguration.Key);
            }

            return _lkgConfigurationCache.Any() ? _lkgConfigurationCache.Keys : null;
        }

        /// <summary>
        /// The last known good configuration or LKG (a configuration retrieved in the past that we were able to successfully validate a token against).
        /// </summary>
        public BaseConfiguration LastKnownGoodConfiguration
        {
            get
            {
                return _lastKnownGoodConfiguration;
            }
            set
            {
                _lastKnownGoodConfiguration = value ?? throw LogHelper.LogArgumentNullException(nameof(value));
                _lastKnownGoodConfigFirstUse = DateTime.UtcNow;

                if (_lkgConfigurationCache == null)
                    _lkgConfigurationCache = new Dictionary<BaseConfiguration, DateTime>(BaseConfigurationComparer);
                
                _lkgConfigurationCache[_lastKnownGoodConfiguration] = DateTime.UtcNow + LastKnownGoodLifetime;

                //remove expired configuration to avoid memory leak 
                var expiredLkgConfiguration = _lkgConfigurationCache.Where(x => x.Value < DateTime.UtcNow).ToArray();

                foreach (KeyValuePair<BaseConfiguration, DateTime> lkgConfiguration in expiredLkgConfiguration)
                {
                    _lkgConfigurationCache.Remove(lkgConfiguration.Key);
                }
            }
        }

        /// <summary>
        /// The length of time that a last known good configuration is valid for.
        /// </summary>
        public TimeSpan LastKnownGoodLifetime
        {
            get { return _lastKnownGoodLifetime; }
            set
            {
                if (value < TimeSpan.Zero)
                    throw LogHelper.LogExceptionMessage(new ArgumentOutOfRangeException(nameof(value), LogHelper.FormatInvariant(LogMessages.IDX10110, value)));

                _lastKnownGoodLifetime = value;
            }
        }

        /// <summary>
        /// The metadata address to retrieve the configuration from.
        /// </summary>
        public string MetadataAddress { get; set; }

        /// <summary>
        /// 5 minutes is the minimum value for automatic refresh. <see cref="AutomaticRefreshInterval"/> can not be set less than this value.
        /// </summary>
        public static readonly TimeSpan MinimumAutomaticRefreshInterval = new TimeSpan(0, 0, 5, 0);

        /// <summary>
        /// 1 second is the minimum time interval that must pass for <see cref="RequestRefresh"/> to  obtain new configuration.
        /// </summary>
        public static readonly TimeSpan MinimumRefreshInterval = new TimeSpan(0, 0, 0, 1);

        /// <summary>
        /// The minimum time between retrievals, in the event that a retrieval failed, or that a refresh was explicitly requested.
        /// </summary>
        public TimeSpan RefreshInterval
        {
            get { return _refreshInterval; }
            set
            {
                if (value < MinimumRefreshInterval)
                    throw LogHelper.LogExceptionMessage(new ArgumentOutOfRangeException(nameof(value), LogHelper.FormatInvariant(LogMessages.IDX10107, LogHelper.MarkAsNonPII(MinimumRefreshInterval), LogHelper.MarkAsNonPII(value))));

                _refreshInterval = value;
            }
        }

        /// <summary>
        /// Indicates whether the last known good feature should be used, true by default.
        /// </summary>
        public bool UseLastKnownGoodConfiguration { get; set; } = true;

        /// <summary>
        /// Indicates whether the last known good configuration is still fresh, depends on when the LKG was first used and it's lifetime.
        /// </summary>
        // The _lastKnownGoodConfiguration private variable is accessed rather than the property (LastKnownGoodConfiguration) as we do not want this access
        // to trigger a change in _lastKnownGoodConfigFirstUse.
        public bool IsLastKnownGoodValid => _lastKnownGoodConfiguration != null && (_lastKnownGoodConfigFirstUse == null || DateTime.UtcNow < _lastKnownGoodConfigFirstUse + LastKnownGoodLifetime);

        /// <summary>
        /// Indicate that the configuration may be stale (as indicated by failing to process incoming tokens).
        /// </summary>
        public abstract void RequestRefresh();
    }
}
