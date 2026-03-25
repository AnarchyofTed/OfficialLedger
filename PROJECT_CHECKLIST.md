# OfficialLedger MVP Checklist

Use this checklist to track build progress phase-by-phase.

## Phase 1: Foundation

### Step 1. Create the project
- [ ] Blazor Web App created
- [ ] Identity auth working
- [ ] Navbar working
- [ ] App runs locally

### Step 2. Set up the database models
- [ ] Model created: `ApplicationUser`
- [ ] Model created: `League`
- [ ] Model created: `Game`
- [ ] `Game` includes `Date`
- [ ] `Game` includes `Sport`
- [ ] `Game` includes `League`
- [ ] `Game` includes `Location`
- [ ] `Game` includes `Fee`
- [ ] `Game` includes `MilesDriven`
- [ ] `Game` includes `Paid`/`Unpaid`
- [ ] `Game` includes `Notes`
- [ ] `ApplicationDbContext` updated
- [ ] Migration added
- [ ] Database created

### Step 3. Decide your first database scope
- [ ] MVP-only tables in database
- [ ] Excluded for MVP: crews
- [ ] Excluded for MVP: assigners
- [ ] Excluded for MVP: multiple payments per game
- [ ] Excluded for MVP: calendar sync
- [ ] Excluded for MVP: rules engine
- [ ] Excluded for MVP: push notifications

---

## Phase 2: Core game tracking

### Step 4. Build the Add Game page
- [ ] Add Game form includes `Game Date`
- [ ] Add Game form includes `Sport`
- [ ] Add Game form includes `League`
- [ ] Add Game form includes `Location`
- [ ] Add Game form includes `Fee Amount`
- [ ] Add Game form includes `Miles Driven`
- [ ] Add Game form includes `Paid`
- [ ] Add Game form includes `Notes`
- [ ] User can submit a game
- [ ] Game saves to database
- [ ] Game tied to logged-in user

### Step 5. Build My Games page
- [ ] My Games shows `Date`
- [ ] My Games shows `Sport`
- [ ] My Games shows `League`
- [ ] My Games shows `Location`
- [ ] My Games shows `Fee`
- [ ] My Games shows `Paid Status`
- [ ] Edit button added
- [ ] Delete button added
- [ ] User can view saved games
- [ ] User can edit saved games
- [ ] User can delete saved games

### Step 6. Protect data by user
- [ ] One user cannot access another user’s games
- [ ] Every game query filters by `UserId`

---

## Phase 3: Dashboard and value

### Step 7. Build the dashboard summary
- [ ] Dashboard card: `Games Worked`
- [ ] Dashboard card: `Total Earnings`
- [ ] Dashboard card: `Outstanding Pay`
- [ ] Dashboard card: `Miles Driven`
- [ ] Section added: recent games
- [ ] Section added: unpaid games
- [ ] Dashboard loads real database data
- [ ] Dashboard data is scoped to logged-in user

### Step 8. Add season filtering
- [ ] Filter available: current year
- [ ] Filter available: all time
- [ ] Dashboard totals can be filtered by year

### Step 9. Add unpaid games view
- [ ] Unpaid games section added
- [ ] Unpaid games grouped by league (if possible)
- [ ] User can quickly see who owes them money

---

## Phase 4: League management

### Step 10. Add a Manage Leagues page
- [ ] User can add league
- [ ] User can edit league
- [ ] User can delete league
- [ ] Add Game page uses user league dropdown

### Step 11. Improve Add Game UX
- [ ] League dropdown implemented
- [ ] Default sport behavior added (when tied to league later)
- [ ] Sensible defaults added
- [ ] Validation messages added
- [ ] Adding a game takes under 10 seconds

---

## Phase 5: Reports and mileage

### Step 12. Create Reports page
- [ ] Reports shows total earnings
- [ ] Reports shows total unpaid
- [ ] Reports shows total mileage
- [ ] Reports shows paid vs unpaid
- [ ] User can view season totals in Reports

### Step 13. Add mileage deduction calculation
- [ ] Mileage rate stored on user OR set as constant
- [ ] Shows total miles
- [ ] Shows estimated deduction
- [ ] User can see mileage deduction estimate

### Step 14. Add export
- [ ] Export games list CSV
- [ ] Export season summary CSV
- [ ] User can export season data

---

## Phase 6: MVP polish

### Step 15. Make it mobile-friendly
- [ ] Add Game page works well on mobile
- [ ] Dashboard cards stack cleanly on mobile
- [ ] My Games list is readable on mobile
- [ ] Tap targets are large enough
- [ ] App is usable on phone without frustration

### Step 16. Add empty states
- [ ] Empty state: “No games yet”
- [ ] Empty state: “Add your first game”
- [ ] Empty state: “No unpaid games”
- [ ] Empty state: “No leagues yet”
- [ ] New users can tell what to do next

### Step 17. Add success and error messages
- [ ] Success: “Game saved”
- [ ] Success: “Game updated”
- [ ] Success: “Game deleted”
- [ ] Error: “Could not save game”
- [ ] Basic user feedback exists everywhere

### Step 18. Clean up branding
- [ ] App title added
- [ ] Favicon/logo placeholder added
- [ ] Consistent blue styling applied
- [ ] Navbar polished
- [ ] App looks intentional

---

## Phase 7: Testing your own workflow

### Step 19. Use it yourself first
- [ ] Entered your own actual games
- [ ] Can add games quickly
- [ ] Dashboard feels useful
- [ ] Totals look right
- [ ] Unpaid section helps
- [ ] You are using it for your own season

### Step 20. Get 3–5 officials to test it
- [ ] Tester can create account
- [ ] Tester can add 3 games
- [ ] Tester can mark one game paid
- [ ] Captured what confused each tester
- [ ] Friction points list created
- [ ] Bug list created
- [ ] Top 3 requested improvements identified
