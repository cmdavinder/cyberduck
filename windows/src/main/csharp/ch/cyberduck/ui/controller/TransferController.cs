﻿// 
// Copyright (c) 2010-2014 Yves Langisch. All rights reserved.
// http://cyberduck.ch/
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// Bug fixes, suggestions and comments should be sent to:
// yves@cyberduck.ch
// 

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ch.cyberduck.core;
using ch.cyberduck.core.formatter;
using ch.cyberduck.core.io;
using ch.cyberduck.core.local;
using ch.cyberduck.core.preferences;
using ch.cyberduck.core.ssl;
using ch.cyberduck.core.threading;
using ch.cyberduck.core.transfer;
using Ch.Cyberduck.Ui.Controller.Threading;
using org.apache.log4j;
using StructureMap;

namespace Ch.Cyberduck.Ui.Controller
{
    public class TransferController : WindowController<ITransferView>, TranscriptListener, CollectionListener
    {
        private static readonly Logger Log = Logger.getLogger(typeof (TransferController).FullName);
        private static readonly object SyncRoot = new Object();
        private static volatile TransferController _instance;

        private readonly IDictionary<Transfer, ProgressController> _transferMap =
            new Dictionary<Transfer, ProgressController>();

        private TransferController()
        {
            View = ObjectFactory.GetInstance<ITransferView>();
            lock (TransferCollection.defaultCollection())
            {
                foreach (Transfer transfer in TransferCollection.defaultCollection())
                {
                    collectionItemAdded(transfer);
                }
            }
            Init();
        }

        public override bool Singleton
        {
            get { return true; }
        }

        public static TransferController Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (SyncRoot)
                    {
                        if (_instance == null)
                            _instance = new TransferController();
                    }
                }
                return _instance;
            }
        }

        public void collectionLoaded()
        {
        }

        public void collectionItemAdded(object obj)
        {
            Invoke(delegate
            {
                Transfer transfer = obj as Transfer;
                ProgressController progressController = new ProgressController(transfer);
                _transferMap.Add(new KeyValuePair<Transfer, ProgressController>(transfer, progressController));
                IProgressView progressView = progressController.View;
                View.AddTransfer(progressView);
                View.SelectTransfer(progressView);
            });
        }

        public void collectionItemRemoved(object obj)
        {
            Invoke(delegate
            {
                Transfer transfer = obj as Transfer;
                if (null != transfer)
                {
                    ProgressController progressController;
                    if (_transferMap.TryGetValue(transfer, out progressController))
                    {
                        View.RemoveTransfer(progressController.View);
                    }
                }
            });
        }

        public void collectionItemChanged(object obj)
        {
        }

        public override void log(bool request, string transcript)
        {
            if (View.TranscriptVisible)
            {
                invoke(new LogAction(this, request, transcript));
            }
        }

        public ProgressController GetController(Transfer transfer)
        {
            ProgressController progressController;
            if (!_transferMap.TryGetValue(transfer, out progressController))
            {
                progressController = new ProgressController(transfer);
                _transferMap.Add(transfer, new ProgressController(transfer));
            }
            return progressController;
        }

        public static bool ApplicationShouldTerminate()
        {
            if (null != _instance)
            {
                //Saving state of transfer window
                PreferencesFactory.get().setProperty("queue.window.open.default", _instance.Visible);
                if (TransferCollection.defaultCollection().numberOfRunningTransfers() > 0)
                {
                    DialogResult result = _instance.QuestionBox(LocaleFactory.localizedString("Transfer in progress"),
                        LocaleFactory.localizedString("There are files currently being transferred. Quit anyway?"), null,
                        String.Format("{0}", LocaleFactory.localizedString("Exit")), true //Cancel
                        );
                    if (DialogResult.OK == result)
                    {
                        // Quit
                        for (int i = 0; i < _instance.getActions().size(); i++)
                        {
                            ((BackgroundAction) _instance.getActions().get(i)).cancel();
                        }
                        return true;
                    }
                    // Cancel
                    return false;
                }
            }
            return true;
        }

        private void Init()
        {
            TransferCollection.defaultCollection().addListener(this);

            PopulateBandwithList();

            View.PositionSizeRestoredEvent += delegate
            {
                View.TranscriptVisible = PreferencesFactory.get().getBoolean("queue.transcript.open");
                View.TranscriptHeight = PreferencesFactory.get().getInteger("queue.transcript.size.height");

                View.ToggleTranscriptEvent += View_ToggleTranscriptEvent;
                View.TranscriptHeightChangedEvent += View_TranscriptHeightChangedEvent;
            };
            View.QueueSize = PreferencesFactory.get().getInteger("queue.maxtransfers");
            View.BandwidthEnabled = false;

            View.ResumeEvent += View_ResumeEvent;
            View.ReloadEvent += View_ReloadEvent;
            View.StopEvent += View_StopEvent;
            View.RemoveEvent += View_RemoveEvent;
            View.CleanEvent += View_CleanEvent;
            View.OpenEvent += View_OpenEvent;
            View.ShowEvent += View_ShowEvent;
            View.TrashEvent += View_TrashEvent;
            View.SelectionChangedEvent += View_SelectionChangedEvent;
            View.BandwidthChangedEvent += View_BandwidthChangedEvent;
            View.QueueSizeChangedEvent += View_QueueSizeChangedEvent;

            View.ValidateResumeEvent += View_ValidateResumeEvent;
            View.ValidateReloadEvent += View_ValidateReloadEvent;
            View.ValidateStopEvent += View_ValidateStopEvent;
            View.ValidateRemoveEvent += View_ValidateRemoveEvent;
            View.ValidateCleanEvent += View_ValidateCleanEvent;
            View.ValidateOpenEvent += View_ValidateOpenEvent;
            View.ValidateShowEvent += View_ValidateShowEvent;
        }

        private void View_TrashEvent()
        {
            foreach (IProgressView progressView in View.SelectedTransfers)
            {
                Transfer transfer = GetTransferFromView(progressView);
                if (!transfer.isRunning())
                {
                    for (int i = 0; i < transfer.getRoots().size(); i++)
                    {
                        TransferItem item = (TransferItem) transfer.getRoots().get(i);
                        try
                        {
                            LocalTrashFactory.get().trash(item.local);
                        }
                        catch (Exception exception)
                        {
                            Log.warn(String.Format("Failure trashing file {0} {1}", item.local, exception.Message));
                        }
                    }
                }
            }
        }

        private void View_TranscriptHeightChangedEvent()
        {
            PreferencesFactory.get().setProperty("queue.transcript.size.height", View.TranscriptHeight);
        }

        private void View_ToggleTranscriptEvent()
        {
            View.TranscriptVisible = !View.TranscriptVisible;
            PreferencesFactory.get().setProperty("queue.transcript.open", View.TranscriptVisible);
        }

        private bool View_ValidateShowEvent()
        {
            return ValidateToolbarItem(delegate(Transfer transfer)
            {
                if (transfer.getLocal() != null)
                {
                    for (int i = 0; i < transfer.getRoots().size(); i++)
                    {
                        TransferItem t = (TransferItem) transfer.getRoots().get(i);
                        if (t.local.exists())
                        {
                            return true;
                        }
                    }
                }
                return false;
            });
        }

        private bool View_ValidateOpenEvent()
        {
            return ValidateToolbarItem(delegate(Transfer transfer)
            {
                if (transfer.getLocal() != null)
                {
                    if (!transfer.isComplete())
                    {
                        return false;
                    }
                    if (!transfer.isRunning())
                    {
                        for (int i = 0; i < transfer.getRoots().size(); i++)
                        {
                            TransferItem item = (TransferItem) transfer.getRoots().get(i);
                            if (item.local.exists())
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            });
        }

        private bool View_ValidateCleanEvent()
        {
            return _transferMap.Count > 0;
        }

        private void View_CleanEvent()
        {
            IList<Transfer> toRemove = new List<Transfer>();
            foreach (KeyValuePair<Transfer, ProgressController> pair in _transferMap)
            {
                Transfer t = pair.Key;
                if (!t.isRunning() && t.isReset() && t.isComplete())
                {
                    TransferCollection.defaultCollection().remove(t);
                    toRemove.Add(t);
                }
            }
            foreach (Transfer t in toRemove)
            {
                _transferMap.Remove(t);
            }
            TransferCollection.defaultCollection().save();
        }

        private bool View_ValidateRemoveEvent()
        {
            return View.SelectedTransfers.Count > 0;
        }

        private bool View_ValidateStopEvent()
        {
            return ValidateToolbarItem(transfer => transfer.isRunning());
        }

        private bool View_ValidateReloadEvent()
        {
            return ValidateToolbarItem(transfer => (transfer.getType().isReloadable() && !transfer.isRunning()));
        }

        /// <summary>
        /// Validates the selected items in the transfer window against the toolbar validator
        /// </summary>
        /// <param name="validate"></param>
        /// <returns>True if one or more of the selected items passes the validation test</returns>
        private bool ValidateToolbarItem(TransferToolbarValidator validate)
        {
            foreach (IProgressView selectedTransfer in View.SelectedTransfers)
            {
                Transfer transfer = GetTransferFromView(selectedTransfer);
                if (validate(transfer))
                {
                    return true;
                }
            }
            return false;
        }

        private bool View_ValidateResumeEvent()
        {
            return ValidateToolbarItem(delegate(Transfer transfer)
            {
                if (transfer.isRunning())
                {
                    return false;
                }
                return !transfer.isComplete();
            });
        }

        private void PopulateBandwithList()
        {
            IList<KeyValuePair<float, string>> list = new List<KeyValuePair<float, string>>();
            list.Add(new KeyValuePair<float, string>(BandwidthThrottle.UNLIMITED,
                LocaleFactory.localizedString("Unlimited Bandwidth", "Preferences")));
            foreach (String option in
                PreferencesFactory.get()
                    .getProperty("queue.bandwidth.options")
                    .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries))
            {
                list.Add(new KeyValuePair<float, string>(Convert.ToInt32(option.Trim()),
                    (SizeFormatterFactory.get(true).format(Convert.ToInt32(option.Trim())) + "/s")));
            }
            View.PopulateBandwidthList(list);
        }

        private void View_QueueSizeChangedEvent()
        {
            PreferencesFactory.get().setProperty("queue.maxtransfers", View.QueueSize);
            TransferQueueFactory.get().resize(PreferencesFactory.get().getInteger("queue.maxtransfers"));
        }

        private void View_BandwidthChangedEvent()
        {
            foreach (IProgressView progressView in View.SelectedTransfers)
            {
                Transfer transfer = GetTransferFromView(progressView);
                transfer.setBandwidth(View.Bandwidth);
                if (transfer.isRunning())
                {
                    BackgroundActionRegistry registry = getActions();
                    // Find matching background task
                    for (int i = 0; i < registry.size(); i++)
                    {
                        if (registry.get(i) is TransferBackgroundAction)
                        {
                            TransferBackgroundAction t = (TransferBackgroundAction) registry.get(i);
                            if (t.getTransfer().Equals(transfer))
                            {
                                TransferSpeedometer meter = t.getMeter();
                                meter.reset();
                            }
                        }
                    }
                }
            }
            UpdateBandwidthPopup();
        }

        private void View_SelectionChangedEvent()
        {
            Log.debug("SelectionChanged");
            UpdateLabels();
            UpdateIcon();
            UpdateBandwidthPopup();
        }

        private void UpdateBandwidthPopup()
        {
            Log.debug("UpdateBandwidthPopup");
            IList<IProgressView> selectedTransfers = View.SelectedTransfers;
            View.BandwidthEnabled = selectedTransfers.Count > 0;

            foreach (IProgressView progressView in View.SelectedTransfers)
            {
                Transfer transfer = GetTransferFromView(progressView);
                if (transfer.getBandwidth().getRate() != BandwidthThrottle.UNLIMITED)
                {
                    View.Bandwidth = transfer.getBandwidth().getRate();
                }
                else
                {
                    View.Bandwidth = BandwidthThrottle.UNLIMITED;
                }
                return;
            }
        }

        private void UpdateIcon()
        {
            IList<IProgressView> selectedTransfers = View.SelectedTransfers;
            if (1 == selectedTransfers.Count)
            {
                Transfer transfer = GetTransferFromView(selectedTransfers[0]);
                if (transfer.getRoots().size() == 1)
                {
                    if (transfer.getLocal() != null)
                    {
                        View.FileIcon = IconCache.Instance.IconForFilename(transfer.getRoot().local.getAbsolute(),
                            IconCache.IconSize.Large);
                    }
                    else
                    {
                        View.FileIcon = IconCache.Instance.IconForPath(transfer.getRoot().remote,
                            IconCache.IconSize.Large);
                    }
                }
                else
                {
                    View.FileIcon = ResourcesBundle.multiple.ToBitmap();
                }
            }
            else
            {
                View.FileIcon = null;
            }
        }

        private void UpdateLabels()
        {
            IList<IProgressView> selectedTransfers = View.SelectedTransfers;
            if (1 == selectedTransfers.Count)
            {
                Transfer transfer = GetTransferFromView(selectedTransfers[0]);
                View.Url = transfer.getRemote().getUrl();
                //Workaround to prevent NullReferenceException
                if (transfer.getLocal() != null)
                {
                    View.Local = transfer.getLocal();
                }
                else
                {
                    View.Local = string.Empty;
                }
            }
            else
            {
                View.Url = string.Empty;
                View.Local = string.Empty;
            }
        }

        private void View_ShowEvent()
        {
            foreach (IProgressView progressView in View.SelectedTransfers)
            {
                Transfer transfer = GetTransferFromView(progressView);
                for (int i = 0; i < transfer.getRoots().size(); i++)
                {
                    TransferItem item = (TransferItem) transfer.getRoots().get(i);
                    RevealServiceFactory.get().reveal(item.local);
                }
            }
        }

        private void View_OpenEvent()
        {
            IList<IProgressView> selected = View.SelectedTransfers;
            if (selected.Count == 1)
            {
                Transfer transfer = GetTransferFromView(selected[0]);

                for (int i = 0; i < transfer.getRoots().size(); i++)
                {
                    TransferItem item = (TransferItem) transfer.getRoots().get(i);
                    Local l = item.local;
                    if (ApplicationLauncherFactory.get().open(l))
                    {
                        break;
                    }
                }
            }
        }

        private void View_RemoveEvent()
        {
            foreach (IProgressView progressView in View.SelectedTransfers)
            {
                Transfer transfer = GetTransferFromView(progressView);
                if (!transfer.isRunning())
                {
                    TransferCollection.defaultCollection().remove(transfer);
                }
            }
            TransferCollection.defaultCollection().save();
        }

        private void View_StopEvent()
        {
            foreach (IProgressView progressView in View.SelectedTransfers)
            {
                Transfer transfer = GetTransferFromView(progressView);
                BackgroundActionRegistry registry = getActions();
                if (transfer.isRunning())
                {
                    // Find matching background task
                    for (int i = 0; i < registry.size(); i++)
                    {
                        if (registry.get(i) is TransferBackgroundAction)
                        {
                            TransferBackgroundAction t = (TransferBackgroundAction) registry.get(i);
                            if (t.getTransfer().Equals(transfer))
                            {
                                t.cancel();
                            }
                        }
                    }
                }
            }
        }

        private void View_ReloadEvent()
        {
            foreach (IProgressView progressView in View.SelectedTransfers)
            {
                Transfer transfer = GetTransferFromView(progressView);
                if (!transfer.isRunning())
                {
                    TransferOptions options = new TransferOptions();
                    options.resumeRequested = false;
                    options.reloadRequested = true;
                    StartTransfer(transfer, options);
                }
            }
        }

        private Transfer GetTransferFromView(IProgressView view)
        {
            foreach (KeyValuePair<Transfer, ProgressController> pair in _transferMap)
            {
                if (pair.Value.View == view) return pair.Key;
            }
            return null;
        }

        private void View_ResumeEvent()
        {
            foreach (IProgressView progressView in View.SelectedTransfers)
            {
                Transfer transfer = GetTransferFromView(progressView);
                if (!transfer.isRunning())
                {
                    TransferOptions options = new TransferOptions();
                    options.resumeRequested = true;
                    options.reloadRequested = false;
                    StartTransfer(transfer, options);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="transfer"></param>
        public void StartTransfer(Transfer transfer)
        {
            StartTransfer(transfer, new TransferOptions());
        }

        public void StartTransfer(Transfer transfer, TransferOptions options)
        {
            StartTransfer(transfer, options, new NoopTransferCallback());
        }

        public void StartTransfer(Transfer transfer, TransferOptions options, TransferCallback callback)
        {
            if (!TransferCollection.defaultCollection().contains(transfer))
            {
                TransferCollection.defaultCollection().add(transfer);
            }
            ProgressController progressController;
            _transferMap.TryGetValue(transfer, out progressController);
            background(new TransferBackgroundAction(this, transfer, options, callback));
        }

        public void TaskbarOverlayIcon(Icon icon, string description)
        {
            Invoke(delegate { View.TaskbarOverlayIcon(icon, description); });
        }

        private class LogAction : WindowMainAction
        {
            private readonly string _msg;
            private readonly bool _request;

            public LogAction(TransferController c, bool request, string msg) : base(c)
            {
                _request = request;
                _msg = msg;
            }

            public override void run()
            {
                ((TransferController) Controller).View.AddTranscriptEntry(_request, _msg);
            }
        }

        private class NoopTransferCallback : TransferCallback
        {
            public void complete(Transfer t)
            {
                ;
            }
        }

        private class TransferBackgroundAction : TransferCollectionBackgroundAction
        {
            private readonly TransferCallback _callback;
            private readonly TransferController _controller;
            private readonly Transfer _transfer;

            public TransferBackgroundAction(TransferController controller, Transfer transfer, TransferOptions options,
                TransferCallback callback)
                : base(
                    controller,
                    SessionFactory.create(transfer.getHost(),
                        new KeychainX509TrustManager(new DefaultTrustManagerHostnameCallback(transfer.getHost())),
                        new KeychainX509KeyManager()), controller.GetController(transfer),
                    controller.GetController(transfer), controller, transfer, options)
            {
                _transfer = transfer;
                _callback = callback;
                _controller = controller;
            }

            public override void init()
            {
                base.init();
                if (PreferencesFactory.get().getBoolean("queue.window.open.transfer.start"))
                {
                    _controller.View.Show();
                    _controller.View.BringToFront();
                }
            }

            public override void finish()
            {
                base.finish();
                if (_transfer.isComplete())
                {
                    _callback.complete(_transfer);
                }
            }

            public override void cleanup()
            {
                base.cleanup();
                if (_transfer.isComplete() && _transfer.isReset())
                {
                    if (PreferencesFactory.get().getBoolean("queue.window.open.transfer.stop"))
                    {
                        if (!(TransferCollection.defaultCollection().numberOfRunningTransfers() > 0))
                        {
                            _controller.View.Close();
                        }
                    }
                }
            }
        }

        private delegate bool TransferToolbarValidator(Transfer transfer);
    }
}