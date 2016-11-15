﻿using FoodLogger.Services;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace FoodLogger.Dialogs
{
    [LuisModel("b403229d-c55a-4bdc-a840-a2b7e25e6634", "d004b0b064694dd1bec537e3629863fb")]
    [Serializable]
    public class RootDialog : LuisDialog<IList<string>>
    {
        [NonSerialized]
        private IMessageActivity _originActivity;
        private IList<string> _foodEntitiesFromLuis;
        private IList<string> _disambiguatedFoods;

        protected override async Task MessageReceived(IDialogContext context, IAwaitable<IMessageActivity> item)
        {
            _originActivity = await item;
            await base.MessageReceived(context, item);
        }

        [LuisIntent("LogMeal")]
        public async Task LogMeal(IDialogContext context, LuisResult result)
        {
            //example message from user "i had 2 bananas a pastry and a coffee"
            _foodEntitiesFromLuis = new List<string>();
            _disambiguatedFoods = new List<string>();

            //enumerate food entities
            foreach (var entity in result.Entities.Where(x => x.Type == "Food"))
            {
                _foodEntitiesFromLuis.Add(entity.Entity);
            }

            await SpecifyFoodAsync(context, null);
        }

        private async Task SpecifyFoodAsync(IDialogContext context, IAwaitable<object> result)
        {
            string disambiguatedFood = null;

            //grab the incoming message text
            if (result != null)
            {
                object awaitedResultObject = await result;

                if (awaitedResultObject is Activity)
                {
                    disambiguatedFood = (awaitedResultObject as Activity).Text;
                }
                else if (awaitedResultObject is string)
                {
                    disambiguatedFood = awaitedResultObject as string;
                }
            }

            //add the incoming message to the disambiguated foods list and remove from the orginal entities list
            if (!string.IsNullOrEmpty(disambiguatedFood))
            {
                //add disambiguated food to global list
                _disambiguatedFoods.Add(disambiguatedFood);
                //remove original food so we don't check it again
                _foodEntitiesFromLuis.Remove(_foodEntitiesFromLuis.First());
            }


            if (_foodEntitiesFromLuis.Count > 0)
            {
                //Create card to present specific food choices 
                IMessageActivity messageButtons = (Activity)context.MakeMessage();
                messageButtons.Recipient = messageButtons.From;
                messageButtons.Type = "message";
                messageButtons.Attachments = new List<Attachment>();
                var disambiguatedFoods = FoodService.GetFoods(_foodEntitiesFromLuis.First());
                PromptForFoodDetails(ref messageButtons, disambiguatedFoods, _foodEntitiesFromLuis.First());
                await context.PostAsync(messageButtons);

                //wait for repsonse
                context.Wait(SpecifyFoodAsync);
            }
            else
            {
                string summaryText = string.Format("You selected {0}, I'll log that for you", string.Join(",", _disambiguatedFoods));

                await context.PostAsync(summaryText);

                context.Done(_disambiguatedFoods);

            }
        }

        private void PromptForFoodDetails(ref IMessageActivity messageActivity, IList<string> disambiguatedFoods, string foodEntity)
        {
            List<CardAction> cardButtons = new List<CardAction>();
            foreach (var disambiguatedFood in disambiguatedFoods)
            {
                cardButtons.Add(new CardAction() { Value = disambiguatedFood, Type = "imBack", Title = disambiguatedFood });
            }
            HeroCard plCard = new HeroCard()
            {
                Title = null,
                Subtitle = string.Format("You said {0}, which one did you mean?", foodEntity),
                Images = null,
                Buttons = cardButtons
            };
            messageActivity.Attachments.Add(plCard.ToAttachment());
        }

    }
}