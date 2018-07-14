﻿namespace Squalr.Source.Updater
{
    using Squalr.Engine;
    using Squalr.Engine.Logging;
    using Squalr.Source.Tasks;
    using Squirrel;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using static Squalr.Engine.TrackableTask;

    /// <summary>
    /// Class for automatically downloading and applying application updates.
    /// </summary>
    static class ApplicationUpdater
    {
        /// <summary>
        /// The url for the Github repository from which updates are fetched.
        /// </summary>
        private static readonly String GithubRepositoryUrl = "https://github.com/Squalr/Squalr";

        /// <summary>
        /// Fetches and applies updates from the Github repository. The application is not restarted.
        /// </summary>
        public static void UpdateApp()
        {
            Task.Run(async () =>
            {
                try
                {
                    using (UpdateManager manager = await UpdateManager.GitHubUpdateManager(ApplicationUpdater.GithubRepositoryUrl))
                    {
                        UpdateInfo updates = await manager.CheckForUpdate();
                        ReleaseEntry lastVersion = updates?.ReleasesToApply?.OrderBy(x => x.Version).LastOrDefault();

                        if (lastVersion == null)
                        {
                            Logger.Log(LogLevel.Info, "Squalr is up to date.");
                            return;
                        }

                        Logger.Log(LogLevel.Info, "New version of Squalr found. Downloading files in background...");

                        TrackableTask<Boolean> downloadTask = TrackableTask<Boolean>
                            .Create("Downloading Updates", out UpdateProgress updateProgress, out CancellationToken cancellationToken)
                            .With(Task.Factory.StartNew<Boolean>(() =>
                            {
                                try
                                {
                                    manager.DownloadReleases(new[] { lastVersion }, (progress) => updateProgress(progress)).RunSynchronously();
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log(LogLevel.Error, "Error downloading updates.", ex);
                                    return false;
                                }

                                return true;
                            }, cancellationToken));

                        TaskTrackerViewModel.GetInstance().TrackTask(downloadTask);

                        if (!downloadTask.Result)
                        {
                            return;
                        }

                        TrackableTask<Boolean> applyReleasesTask = TrackableTask<Boolean>
                            .Create("Applying Releases", out updateProgress, out cancellationToken)
                            .With(Task.Factory.StartNew<Boolean>(() =>
                            {
                                try
                                {
                                    manager.ApplyReleases(updates, (progress) => updateProgress(progress)).RunSynchronously();
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log(LogLevel.Error, "Error applying releases.", ex);
                                    return false;
                                }

                                return true;
                            }, cancellationToken));

                        TaskTrackerViewModel.GetInstance().TrackTask(applyReleasesTask);

                        if (!applyReleasesTask.Result)
                        {
                            return;
                        }

                        TrackableTask<Boolean> updateTask = TrackableTask<Boolean>
                            .Create("Updating", out updateProgress, out cancellationToken)
                            .With(Task.Factory.StartNew<Boolean>(() =>
                            {
                                try
                                {
                                    manager.UpdateApp((progress) => updateProgress(progress)).RunSynchronously();
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log(LogLevel.Error, "Error applying updates.", ex);
                                    return false;
                                }

                                return true;
                            }, cancellationToken));

                        TaskTrackerViewModel.GetInstance().TrackTask(updateTask);

                        if (!updateTask.Result)
                        {
                            return;
                        }

                        Logger.Log(LogLevel.Info, "New Squalr version downloaded. Restart the application to apply updates.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "Error updating Squalr.", ex);
                }
            });
        }
    }
    //// End class
}
//// End namespace