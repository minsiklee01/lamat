using System;
using System.Collections.Generic;
using System.Text;
using lamat.Models;

// manage position/word practice progress status

namespace lamat.Services
{
    public class KeySequenceSessionService
    {
        private List<KeySequencePracticeItem> _items = new();

        public int CurrentItemIndex { get; private set; } = 0;
        public int CurrentStepIndex { get; private set; } = 0;

        public void LoadItems(List<KeySequencePracticeItem> items)
        {
            _items = items;
            CurrentItemIndex = 0;
            CurrentStepIndex = 0;
        }

        public KeySequencePracticeItem? GetCurrentItem()
        {
            if (_items.Count == 0 || CurrentItemIndex >= _items.Count)
            {
                return null;
            }
            return _items[CurrentItemIndex];
        }

        // offset 0 = current item, negative = already-completed items, positive = upcoming items.
        public KeySequencePracticeItem? PeekItem(int offset)
        {
            int idx = CurrentItemIndex + offset;
            return idx >= 0 && idx < _items.Count ? _items[idx] : null;
        }

        public KeyStep? GetCurrentStep()
        {
            var item = GetCurrentItem();

            if (item == null || item.Steps.Count == 0 || CurrentStepIndex >= item.Steps.Count)
            {
                return null;
            }

            return item.Steps[CurrentStepIndex];
        }

        public void AdvanceStep()
        {
            CurrentStepIndex++;
        }

        public void RevertStep()
        {
            if (CurrentStepIndex > 0)
                CurrentStepIndex--;
        }

        public void AdvanceItem()
        {
            CurrentItemIndex++;
            CurrentStepIndex = 0;
        }

        public bool HasMoreItems()
        {
            return CurrentItemIndex < _items.Count;
        }

        public int TotalItemCount => _items.Count;

    }
}
