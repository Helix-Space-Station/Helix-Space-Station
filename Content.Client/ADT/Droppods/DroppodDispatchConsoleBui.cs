using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Administration.UI.CustomControls;
using Content.Shared.ADT.Droppods;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.ADT.Droppods;

[UsedImplicitly]
public sealed class DroppodDispatchConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private Label? _statusLabel;
    private BoxContainer? _cargoList;
    private BoxContainer? _ghostList;
    private BoxContainer? _beaconList;
    private Label? _selectedLabel;
    private Button? _launchButton;

    private readonly HashSet<NetEntity> _selectedCargo = new();
    private readonly HashSet<EntProtoId> _selectedGhosts = new();
    private NetEntity? _selectedBeacon;
    private string? _selectedBeaconName;

    private DroppodDispatchConsoleBuiState? _state;

    protected override void Open()
    {
        base.Open();
        TryInitWindow();
        UpdateState(State);
    }

    protected override void UpdateState(BoundUserInterfaceState? state)
    {
        if (state is not DroppodDispatchConsoleBuiState s)
            return;

        _state = s;
        TryInitWindow();

        _selectedCargo.RemoveWhere(id => s.Cargo.TrueForAll(c => c.Uid != id));
        _selectedGhosts.RemoveWhere(id => s.GhostOptions.TrueForAll(g => g.Prototype != id));

        if (_selectedCargo.Count == 0)
        {
            foreach (var cargo in s.Cargo)
                _selectedCargo.Add(cargo.Uid);
        }

        if (_selectedBeacon.HasValue && s.Beacons.TrueForAll(b => b.Uid != _selectedBeacon.Value))
        {
            _selectedBeacon = null;
            _selectedBeaconName = null;
        }

        RebuildLists();
        RefreshStatus();

        if (!_window!.IsOpen)
            _window.OpenCentered();
    }

    private void TryInitWindow()
    {
        if (_window != null)
            return;

        _window = new DefaultWindow
        {
            Title = Loc.GetString("droppod-dispatch-console-title"),
            MinSize = new Vector2(560, 460),
            SetSize = new Vector2(560, 460),
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(8),
        };

        root.AddChild(BuildLeft());
        root.AddChild(BuildRight());
        _window.Contents.AddChild(root);
        _window.OnClose += Close;
    }

    private Control BuildLeft()
    {
        var panel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinSize = new Vector2(250, 0),
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 10, 0),
        };

        _statusLabel = new Label { Margin = new Thickness(0, 0, 0, 6) };
        panel.AddChild(_statusLabel);

        panel.AddChild(Header("droppod-dispatch-console-cargo-header"));
        _cargoList = AddScroll(panel);

        panel.AddChild(new HSeparator { Margin = new Thickness(0, 4, 0, 6) });
        panel.AddChild(Header("droppod-dispatch-console-ghost-header"));
        _ghostList = AddScroll(panel);

        return panel;
    }

    private Control BuildRight()
    {
        var panel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinSize = new Vector2(250, 0),
            HorizontalExpand = true,
        };

        panel.AddChild(Header("droppod-dispatch-console-beacon-header"));
        _beaconList = AddScroll(panel, expand: true);

        panel.AddChild(new HSeparator { Margin = new Thickness(0, 4, 0, 6) });
        _selectedLabel = new Label { Margin = new Thickness(0, 0, 0, 8) };
        panel.AddChild(_selectedLabel);

        _launchButton = new Button
        {
            Text = Loc.GetString("droppod-dispatch-console-launch"),
            Disabled = true,
            HorizontalExpand = true,
            ModulateSelfOverride = Color.FromHex("#3d7a3d"),
        };
        _launchButton.OnPressed += _ => OnLaunchPressed();
        panel.AddChild(_launchButton);

        return panel;
    }

    private static Label Header(string loc)
    {
        return new Label
        {
            Text = Loc.GetString(loc),
            FontColorOverride = Color.FromHex("#aaaaaa"),
            Margin = new Thickness(0, 0, 0, 4),
        };
    }

    private static BoxContainer AddScroll(BoxContainer parent, bool expand = false)
    {
        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            MinSize = new Vector2(0, expand ? 0 : 90),
            Margin = new Thickness(0, 0, 0, 4),
        };
        var list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        scroll.AddChild(list);
        parent.AddChild(scroll);
        return list;
    }

    private void RebuildLists()
    {
        if (_state == null)
            return;

        if (_cargoList != null)
        {
            _cargoList.RemoveAllChildren();
            if (_state.Cargo.Count == 0)
            {
                _cargoList.AddChild(new Label
                {
                    Text = Loc.GetString("droppod-dispatch-console-empty"),
                    FontColorOverride = Color.Gray,
                });
            }
            else
            {
                foreach (var cargo in _state.Cargo)
                {
                    var id = cargo.Uid;
                    var check = new CheckBox
                    {
                        Text = cargo.IsMob ? $"{cargo.Name} ★" : cargo.Name,
                        Pressed = _selectedCargo.Contains(id),
                        ToggleMode = true,
                        HorizontalExpand = true,
                    };
                    check.OnPressed += _ =>
                    {
                        if (check.Pressed)
                            _selectedCargo.Add(id);
                        else
                            _selectedCargo.Remove(id);
                        RefreshStatus();
                    };
                    _cargoList.AddChild(check);
                }
            }
        }

        if (_ghostList != null)
        {
            _ghostList.RemoveAllChildren();
            if (_state.GhostOptions.Count == 0)
            {
                _ghostList.AddChild(new Label
                {
                    Text = Loc.GetString("droppod-dispatch-console-empty"),
                    FontColorOverride = Color.Gray,
                });
            }
            else
            {
                foreach (var option in _state.GhostOptions)
                {
                    var proto = option.Prototype;
                    var check = new CheckBox
                    {
                        Text = option.Name,
                        Pressed = _selectedGhosts.Contains(proto),
                        ToggleMode = true,
                        HorizontalExpand = true,
                    };
                    check.OnPressed += _ =>
                    {
                        if (check.Pressed)
                            _selectedGhosts.Add(proto);
                        else
                            _selectedGhosts.Remove(proto);
                        RefreshStatus();
                    };
                    _ghostList.AddChild(check);
                }
            }
        }

        if (_beaconList != null)
        {
            _beaconList.RemoveAllChildren();
            foreach (var beacon in _state.Beacons)
            {
                var b = beacon;
                var btn = new Button
                {
                    Text = b.Name,
                    HorizontalExpand = true,
                    Margin = new Thickness(0, 0, 0, 2),
                    ModulateSelfOverride = _selectedBeacon == b.Uid ? Color.Orange : null,
                };
                btn.OnPressed += _ =>
                {
                    _selectedBeacon = b.Uid;
                    _selectedBeaconName = b.Name;
                    RebuildLists();
                    RefreshStatus();
                };
                _beaconList.AddChild(btn);
            }
        }
    }

    private void RefreshStatus()
    {
        if (_state == null || _statusLabel == null)
            return;

        if (!_state.Powered)
        {
            _statusLabel.Text = Loc.GetString("droppod-dispatch-console-status-unpowered");
            _statusLabel.FontColorOverride = Color.Red;
        }
        else if (_state.CooldownRemaining > 0)
        {
            _statusLabel.Text = Loc.GetString("droppod-dispatch-console-cooldown", ("seconds", _state.CooldownRemaining));
            _statusLabel.FontColorOverride = Color.Yellow;
        }
        else if (!_state.CanLaunch)
        {
            _statusLabel.Text = Loc.GetString("droppod-dispatch-console-status-not-ready");
            _statusLabel.FontColorOverride = Color.Yellow;
        }
        else
        {
            _statusLabel.Text = Loc.GetString("droppod-dispatch-console-status-ready");
            _statusLabel.FontColorOverride = Color.LimeGreen;
        }

        if (_selectedLabel != null)
        {
            _selectedLabel.Text = _selectedBeaconName != null
                ? _selectedBeaconName
                : Loc.GetString("droppod-dispatch-console-no-beacon");
            _selectedLabel.FontColorOverride = _selectedBeaconName != null ? Color.Orange : Color.Gray;
        }

        var hasPayload = _selectedCargo.Count > 0 || _selectedGhosts.Count > 0;
        if (_launchButton != null)
        {
            _launchButton.Disabled = !_state.CanLaunch
                || !_state.Powered
                || _selectedBeacon == null
                || !hasPayload;
        }
    }

    private void OnLaunchPressed()
    {
        if (_selectedBeacon == null)
            return;

        SendMessage(new DroppodDispatchLaunchMessage
        {
            TargetBeacon = _selectedBeacon.Value,
            Cargo = _selectedCargo.ToList(),
            ExtraSpawns = _selectedGhosts.ToList(),
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Close();
    }
}
