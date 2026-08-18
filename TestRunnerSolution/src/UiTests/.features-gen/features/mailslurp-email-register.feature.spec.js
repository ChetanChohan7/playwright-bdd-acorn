// Generated from: features/mailslurp-email-register.feature
import { test } from "playwright-bdd";

test.describe('Email registration link', () => {

  test('Register a new user and follow the link in the verification email', { tag: ['@mailslurp', '@email'] }, async ({ Given, When, Then, And, page }) => { 
    await Given('a fresh MailSlurp inbox'); 
    await And('I am on the registration page', null, { page }); 
    await When('I register using the MailSlurp inbox email address', null, { page }); 
    await And('I wait for the latest email sent to the inbox'); 
    await Then('the email body should contain a link'); 
    await When('I navigate to the extracted email link', null, { page }); 
    await Then('the linked page from the email should load successfully'); 
  });

});

// == technical section ==

test.use({
  $test: [({}, use) => use(test), { scope: 'test', box: true }],
  $uri: [({}, use) => use('features/mailslurp-email-register.feature'), { scope: 'test', box: true }],
  $bddFileData: [({}, use) => use(bddFileData), { scope: "test", box: true }],
});

const bddFileData = [ // bdd-data-start
  {"pwTestLine":6,"pickleLine":8,"tags":["@mailslurp","@email"],"steps":[{"pwStepLine":7,"gherkinStepLine":9,"keywordType":"Context","textWithKeyword":"Given a fresh MailSlurp inbox","stepMatchArguments":[]},{"pwStepLine":8,"gherkinStepLine":10,"keywordType":"Context","textWithKeyword":"And I am on the registration page","stepMatchArguments":[]},{"pwStepLine":9,"gherkinStepLine":11,"keywordType":"Action","textWithKeyword":"When I register using the MailSlurp inbox email address","stepMatchArguments":[]},{"pwStepLine":10,"gherkinStepLine":12,"keywordType":"Action","textWithKeyword":"And I wait for the latest email sent to the inbox","stepMatchArguments":[]},{"pwStepLine":11,"gherkinStepLine":13,"keywordType":"Outcome","textWithKeyword":"Then the email body should contain a link","stepMatchArguments":[]},{"pwStepLine":12,"gherkinStepLine":14,"keywordType":"Action","textWithKeyword":"When I navigate to the extracted email link","stepMatchArguments":[]},{"pwStepLine":13,"gherkinStepLine":15,"keywordType":"Outcome","textWithKeyword":"Then the linked page from the email should load successfully","stepMatchArguments":[]}]},
]; // bdd-data-end