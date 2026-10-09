SHARED_RULES = """The resume is the official, authoritative record. The site data was written by hand and may be out of date. Your job is to find where they differ in substance and propose edits to the SITE DATA so it agrees with the resume.

RULES:
- Propose only "add" and "update". Never propose removing anything: a resume leaving something out does not mean the site should.
- Only propose a change that has real support in the resume. Never invent employers, titles, dates, numbers, technologies, or accomplishments, and never infer one.
- Absence is not contradiction. Never drop an existing list entry (a bullet, technology, or keyword) or rewrite existing text just because the resume doesn't mention it. Remove or replace something only when the resume states something different about that same thing, for example a different database for the same project.
- Differences in wording alone are not worth a suggestion. Propose a change when a fact differs (a title, an employer name, a date, a number, a technology, a responsibility) or when the resume holds something real the site lacks.
- On an update, set only the fields that should change and leave every other field null. A list field (achievements, tech_stack, keywords) is an edit, not a replacement: "add" lists the entries to add, and "remove" lists existing entries to take out, copied exactly. Existing entries you don't name stay. To fix an entry, remove the old one and add the corrected one.
- Match the style of the existing site rows (year format, level of detail, voice).
- "evidence" must be ONE contiguous passage copied exactly from the resume text, character for character, no longer than about 250 characters. Do not paraphrase it and do not join separate passages. A suggestion whose evidence isn't found verbatim in the resume is discarded.
- "rationale" is one plain sentence about what differs.
- If nothing needs to change, return an empty list. Returning nothing is better than returning a weak suggestion.
"""

COMPARE_WORK_PROMPT = SHARED_RULES + """
YOUR SECTION: work experience.

The site stores one row per role (employer + title + dates), labeled W1, W2, and so on. The resume may group several roles under one employer heading, for example one employer with a sequence of titles and a combined date range. Treat each role separately and match it to the site row with the same employer, title, and dates.
- If a resume role matches a site row, propose an "update" only for fields that differ in substance.
- If a resume role has no matching site row, propose an "add" with employer, title, start_year, end_year, summary, and achievements filled from the resume.
- Employer name formatting counts: if the site says "Campus Works" and the resume says "CampusWorks LLC", the resume's version is the official one.
- Achievements are the resume's bullets for that role. Keep a site bullet if it is still accurate, fix it if the resume gives a different fact, and add resume bullets the site lacks.
- The resume has no per-role summaries. The site's summary is a hand-written overview. Leave summary null on an update unless it states a fact the resume contradicts, and then correct only that fact. Never use a resume bullet as the summary. For an add, write a one-sentence summary from that role's bullets.
- start_year and end_year are four-digit years. For a role that hasn't ended, end_year is "present".
- One resume entry may cover several site rows (the same employer with a sequence of titles). Its bullets usually can't be attributed to one title, so don't copy them into a single row. Add a bullet to a row only when it clearly belongs to that title or period.
- Ignore site rows with no resume counterpart.
"""

COMPARE_PROJECTS_PROMPT = SHARED_RULES + """
YOUR SECTION: projects.

The site stores one row per project, labeled P1, P2, and so on. The resume describes projects in two places: a Personal Projects section, and inside work bullets (a bullet may name a distinct system or initiative).
- Propose an "add" only when the resume describes a distinct, nameable project that has no matching site row. Be conservative: a single bullet about routine work is not a project. Give a title, role, a one or two sentence summary, tech_stack, and years.
- Propose an "update" only when a site row clearly is the same project and the resume gives a different fact or a technology the site row lacks.
- Work history entries are handled separately. Do not propose projects for whole jobs.
"""

COMPARE_INFORMATION_PROMPT = SHARED_RULES + """
YOUR SECTION: general information.

The site also keeps a set of free-text entries, labeled I1, I2, and so on, that a chatbot searches to answer questions about Samuel. They are written in first person. The resume holds facts that belong there if they are not already covered: certifications (with dates), education (degree, school, honors), the skills list, and claims in the summary.
- Propose an "add" for a fact the existing entries do not already cover. Write the text in first person, plain and factual, in the voice of the existing entries, and give 3 to 8 short lowercase keywords. One topic per entry (for example, one entry for certifications, one for education).
- Opinions, preferences, working style, and philosophy are not facts the resume can contradict. Leave those entries alone.
- Propose an "update" only when an existing entry states a checkable fact (an employer, date, number, or the technology used on a named project) that the resume contradicts. Give the complete text with only that fact corrected and everything else, including the voice, unchanged.
- Do not propose entries about jobs or projects: work history and projects are handled separately.
- Do not add anything personal beyond what the resume states (no address, phone, or salary).
"""
