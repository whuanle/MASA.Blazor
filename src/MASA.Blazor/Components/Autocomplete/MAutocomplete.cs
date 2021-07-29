﻿using BlazorComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace MASA.Blazor
{
    public class MAutocomplete<TItem, TValue> : MSelect<TItem, TValue>
    {
        private Func<TItem, string, bool> _filter;

        protected List<TItem> FilteredItems => Items.Where(r => string.IsNullOrEmpty(QueryText) || Filter(r, QueryText)).ToList();

        protected int FilteredItemsCount => Items.Count(r => string.IsNullOrEmpty(QueryText) || Filter(r, QueryText));

        protected string QueryText { get; set; }

        protected int HighlightIndex { get; set; } = -1;

        protected TItem SelectedItem { get; set; }

        protected List<TItem> SelectedItems { get; set; }

        protected Timer Timer { get; set; }

        protected string WaitingQueryText { get; set; }

        [Parameter]
        public Func<TItem, string, bool> Filter
        {
            get
            {
                if (_filter == null)
                {
                    _filter = (item, query) => ItemText(item).ToLower().IndexOf(query.ToLower()) > -1;
                }

                return _filter;
            }
            set
            {
                _filter = value;
            }
        }

        [Parameter]
        public EventCallback<string> OnInput { get; set; }

        [Parameter]
        public double Interval { get; set; } = 500;

        protected override void SetComponentClass()
        {
            base.SetComponentClass();

            HasBody = true;
            IsAutocomplete = true;

            AbstractProvider
                .Merge<BMenu, MCascaderMenu>(props =>
                {
                    props[nameof(MCascaderMenu.OffsetY)] = true;
                    props[nameof(MCascaderMenu.ActivatorStyle)] = "display:flex";
                    props[nameof(MCascaderMenu.CloseOnContentClick)] = !Multiple;
                })
                .Apply<ISelectBody, MAutocompleteSelectBody<TItem>>(props =>
                {
                    props[nameof(MAutocompleteSelectBody<TItem>.Items)] = FilteredItems;
                    props[nameof(MAutocompleteSelectBody<TItem>.ItemText)] = ItemText;
                    props[nameof(MAutocompleteSelectBody<TItem>.OnItemClick)] = EventCallback.Factory.Create<TItem>(this, item =>
                    {
                        HandleItemClick(item);
                    });
                    props[nameof(MAutocompleteSelectBody<TItem>.QueryText)] = QueryText;

                    if (FilteredItemsCount == 1)
                    {
                        HighlightIndex = 0;
                    }

                    props[nameof(MAutocompleteSelectBody<TItem>.HighlightIndex)] = HighlightIndex;
                    props[nameof(MAutocompleteSelectBody<TItem>.SelectedItem)] = SelectedItem;
                    props[nameof(MAutocompleteSelectBody<TItem>.Multiple)] = Multiple;
                    props[nameof(MAutocompleteSelectBody<TItem>.SelectedItems)] = SelectedItems;
                })
                .Apply<BProcessLinear, MProcessLinear>(props =>
                 {
                     props[nameof(MProcessLinear.Color)] = "primary";
                     props[nameof(MProcessLinear.Indeterminate)] = true;
                     props[nameof(MProcessLinear.Absolute)] = true;
                     props[nameof(MProcessLinear.Height)] = 2;
                     props[nameof(MProcessLinear.Dark)] = true;
                 });

            CssProvider
                .Merge(cssBuilder =>
                {
                    cssBuilder
                        .Add("m-autocomplete");
                });
        }

        protected override void OnParametersSet()
        {
            base.OnParametersSet();

            if (Multiple)
            {
                SelectedItems = Items.Where(r => Values.Contains(ItemValue(r))).ToList();
                _text = SelectedItems.Select(r => ItemText(r)).ToList();
            }
            else
            {
                SelectedItem = Items.FirstOrDefault(r => EqualityComparer<TValue>.Default.Equals(Value, ItemValue(r)));
                ValueText = SelectedItem == null ? string.Empty : ItemText(SelectedItem);
            }
        }

        protected override void OnInitialized()
        {
            if (Timer == null && Interval > 0)
            {
                Timer = new Timer();
                Timer.Interval = Interval;
                Timer.Elapsed += Timer_Elapsed;
            }
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (WaitingQueryText != null)
            {
                var waitingQueryText = WaitingQueryText;
                WaitingQueryText = null;
                InvokeAsync(async () =>
                {
                    await OnInput.InvokeAsync(waitingQueryText);

                    if (WaitingQueryText == null)
                    {
                        Timer.Stop();
                    }
                });
            }
        }

        private void HandleItemClick(TItem item)
        {
            if (Multiple)
            {
                if (SelectedItems.Contains(item))
                {
                    SelectedItems.Remove(item);
                    _text.Remove(ItemText(item));
                }
                else
                {
                    SelectedItems.Add(item);
                    _text.Add(ItemText(item));
                }
            }
            else
            {
                SelectedItem = item;
                OnBlur();
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await JsInvokeAsync(JsInteropConstants.PreventDefaultOnArrowUpDown, InputRef);
            }
        }

        protected override async Task Click(MouseEventArgs args)
        {
            await base.Click(args);
            QueryText = string.Empty;

            if (SelectedItem != null)
            {
                var activeIndex = FilteredItems.IndexOf(SelectedItem);
                HighlightIndex = activeIndex;
            }

            await InputRef.FocusAsync();
        }

        protected override void HandleOnBlur(FocusEventArgs args)
        {
            base.HandleOnBlur(args);
            OnBlur();
        }

        protected void OnBlur()
        {
            HighlightIndex = -1;
            ValueText = SelectedItem == null ? string.Empty : ItemText(SelectedItem);
            Visible = false;

            if (Multiple)
            {
                Values = SelectedItems.Select(r => ItemValue(r)).ToList();
                if (ValuesChanged.HasDelegate)
                {
                    ValuesChanged.InvokeAsync(Values);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(ValueText))
                {
                    Value = default;
                }
                else
                {
                    Value = ItemValue(SelectedItem);
                }

                if (ValueChanged.HasDelegate)
                {
                    ValueChanged.InvokeAsync(Value);
                }
            }
        }

        protected override async Task HandleOnInputAsync(ChangeEventArgs args)
        {
            ValueText = args.Value.ToString();
            QueryText = ValueText;
            HighlightIndex = -1;
            Visible = true;

            if (OnInput.HasDelegate)
            {
                WaitingQueryText = QueryText;
                if (Timer != null && Interval > 0)
                {
                    if (!Timer.Enabled)
                    {
                        Timer.Start();
                    }
                    else
                    {
                        //restart
                        Timer.Stop();
                        Timer.Start();
                    }
                }
                else
                {
                    await OnInput.InvokeAsync(WaitingQueryText);
                }
            }

            await base.HandleOnInputAsync(args);
        }

        protected override async Task HandleOnKeyDownAsync(KeyboardEventArgs args)
        {
            switch (args.Code)
            {
                case "ArrowUp":
                    if (HighlightIndex > 0)
                    {
                        HighlightIndex--;
                    }
                    else
                    {
                        HighlightIndex = FilteredItemsCount - 1;
                    }
                    break;
                case "ArrowDown":
                    if (HighlightIndex < FilteredItemsCount - 1)
                    {
                        HighlightIndex++;
                    }
                    else
                    {
                        HighlightIndex = 0;
                    }
                    break;
                case "Enter":
                    Visible = true;

                    if (HighlightIndex > -1 && HighlightIndex < FilteredItemsCount)
                    {
                        await Task.Yield();
                        HandleItemClick(FilteredItems[HighlightIndex]);
                    }
                    break;
                case "Backspace":
                    if (Multiple)
                    {
                        if (string.IsNullOrEmpty(ValueText) && SelectedItems.Count > 0)
                        {
                            var lastIndex = SelectedItems.Count - 1;
                            SelectedItems.RemoveAt(lastIndex);
                            _text.RemoveAt(lastIndex);
                        }
                    }
                    else
                    {
                        if (ValueText.Length == 1)
                        {
                            SelectedItem = default;
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            Timer?.Dispose();
            base.Dispose(disposing);
        }
    }
}
