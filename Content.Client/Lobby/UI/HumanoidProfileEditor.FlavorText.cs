using Content.Shared.SD;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private bool _allowFlavorText;

    private FlavorText.FlavorText? _flavorText;
    private TextEdit? _flavorTextEdit;

    /// <summary>
    /// Refreshes the flavor text editor status.
    /// </summary>
    public void RefreshFlavorText()
    {
        if (_allowFlavorText)
        {
            if (_flavorText != null)
                return;

            _flavorText = new FlavorText.FlavorText();
            TabContainer.AddChild(_flavorText);
            TabContainer.SetTabTitle(TabContainer.ChildCount - 1, Loc.GetString("humanoid-profile-editor-flavortext-tab"));
            _flavorTextEdit = _flavorText.CFlavorTextInput;

            // SD-ERPStatus-Start
            _erpStatus = _flavorText.CERPStatusOption;
            _erpStatus.AddItem(Loc.GetString("humanoid-erp-status-no"), (int) EnumERPStatus.NO);
            _erpStatus.AddItem(Loc.GetString("humanoid-erp-status-half"), (int) EnumERPStatus.HALF);
            _erpStatus.AddItem(Loc.GetString("humanoid-erp-status-full"), (int) EnumERPStatus.FULL);
            _erpStatus.OnItemSelected += args =>
            {
                if (Profile is null)
                    return;

                _erpStatus.SelectId(args.Id);
                Profile = Profile.WithERPStatus((EnumERPStatus) args.Id);
                IsDirty = true;
            };
            // SD-ERPStatus-End

            _flavorText.OnFlavorTextChanged += OnFlavorTextChange;
            _flavorText.OnHeadshotUrlChanged += OnHeadshotUrlChange;
            _flavorText.OnPreviewRequested += OnFlavorPreviewRequested;
        }
        else
        {
            if (_flavorText == null)
                return;

            TabContainer.RemoveChild(_flavorText);
            _flavorText.OnFlavorTextChanged -= OnFlavorTextChange;
            _flavorText.OnHeadshotUrlChanged -= OnHeadshotUrlChange;
            _flavorText.OnPreviewRequested -= OnFlavorPreviewRequested;
            _flavorText.Dispose();
            _flavorTextEdit?.Dispose();
            _flavorTextEdit = null;
            _flavorText = null;
        }
    }

    private void OnFlavorTextChange(string content)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithFlavorText(content);
        SetDirty();
    }

    private void UpdateFlavorTextEdit()
    {
        if (_flavorTextEdit != null)
        {
            _flavorTextEdit.TextRope = new Rope.Leaf(Profile?.FlavorText ?? "");
        }

        if (_flavorText != null)
        {
            _flavorText.CHeadshotUrlInput.Text = Profile?.HeadshotUrl ?? "";
        }
    }

    // SD-ERPStatus-Start
    private void UpdateERPStatus()
    {
        if (_erpStatus != null)
        {
            _erpStatus.SelectId((int) (Profile?.ERPStatus ?? EnumERPStatus.NO));
        }
    }
    // SD-ERPStatus-End
}