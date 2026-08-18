// Generated from: features/mailslurp-sms-invite.feature
import { test } from "playwright-bdd";

test.describe('SMS invite link', () => {

  test('Send an invite and follow the link in the SMS', { tag: ['@mailslurp', '@sms'] }, async ({ Given, When, Then, And, page }) => { 
    await Given('I am on the invite page', null, { page }); 
    await When('I click the button to send an SMS invite', null, { page }); 
    await And('I wait for the latest SMS sent to the MailSlurp phone number'); 
    await Then('the SMS body should contain a link'); 
    await When('I navigate to the extracted SMS link', null, { page }); 
    await Then('the linked page from the SMS should load successfully'); 
  });

});

// == technical section ==

test.use({
  $test: [({}, use) => use(test), { scope: 'test', box: true }],
  $uri: [({}, use) => use('features/mailslurp-sms-invite.feature'), { scope: 'test', box: true }],
  $bddFileData: [({}, use) => use(bddFileData), { scope: "test", box: true }],
});

const bddFileData = [ // bdd-data-start
  {"pwTestLine":6,"pickleLine":8,"tags":["@mailslurp","@sms"],"steps":[{"pwStepLine":7,"gherkinStepLine":9,"keywordType":"Context","textWithKeyword":"Given I am on the invite page","stepMatchArguments":[]},{"pwStepLine":8,"gherkinStepLine":10,"keywordType":"Action","textWithKeyword":"When I click the button to send an SMS invite","stepMatchArguments":[]},{"pwStepLine":9,"gherkinStepLine":11,"keywordType":"Action","textWithKeyword":"And I wait for the latest SMS sent to the MailSlurp phone number","stepMatchArguments":[]},{"pwStepLine":10,"gherkinStepLine":12,"keywordType":"Outcome","textWithKeyword":"Then the SMS body should contain a link","stepMatchArguments":[]},{"pwStepLine":11,"gherkinStepLine":13,"keywordType":"Action","textWithKeyword":"When I navigate to the extracted SMS link","stepMatchArguments":[]},{"pwStepLine":12,"gherkinStepLine":14,"keywordType":"Outcome","textWithKeyword":"Then the linked page from the SMS should load successfully","stepMatchArguments":[]}]},
]; // bdd-data-end